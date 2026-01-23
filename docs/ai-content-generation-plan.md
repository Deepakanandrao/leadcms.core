# AI-assisted content generation plan for LeadCMS

## 1) What already exists in this codebase

- **AI plugin**: The plugin already provides content generation, translation, and image generation.
  - [plugins/LeadCMS.Plugin.AI/Services/ContentGenerationService.cs](plugins/LeadCMS.Plugin.AI/Services/ContentGenerationService.cs)
  - [plugins/LeadCMS.Plugin.AI/Services/OpenAIProviderService.cs](plugins/LeadCMS.Plugin.AI/Services/OpenAIProviderService.cs)
  - [plugins/LeadCMS.Plugin.AI/Controllers/ContentGenerationController.cs](plugins/LeadCMS.Plugin.AI/Controllers/ContentGenerationController.cs)
- **MDX analysis**: There is an MDX component analyzer that can extract supported components per content type.
  - [src/LeadCMS/Services/MdxComponentParserService.cs](src/LeadCMS/Services/MdxComponentParserService.cs)
- **Content types and formats**: Content format is already enumerated and used in prompts.
  - [src/LeadCMS/Entities/ContentType.cs](src/LeadCMS/Entities/ContentType.cs)

This is a strong base. The missing pieces are mainly **knowledge retrieval**, **site-level topic profile**, **media reuse**, and **multi-source context ingestion**.

---

## 2) Target architecture (high level)

**Goal**: Generate draft content that matches the chosen content type (JSON/MDX), reuses site-specific language and components, references relevant media, and stays aligned with a site’s goals.

### 2.1 Components

1. **Site Profile** (new): High-level summary of each site’s topic, goals, audience, and voice.
2. **Knowledge Base** (new): Uploaded files, curated docs, brand guidelines, and internal knowledge.
3. **File Storage Sync** (new): OpenAI File Storage synced from CMS content changes.
4. **Context Builder** (new service): Collects relevant content samples, MDX components, media assets, and top-K knowledge chunks.
5. **AI Orchestrator** (extension of existing service): Builds system/user prompts, validates JSON/MDX, and logs context.

### 2.2 MVP-first: OpenAI File Search over local RAG

For a fast, reliable MVP, use **OpenAI File Search** instead of building a local vector store. It reduces engineering time and lets you ship in days.

**Why this works for now**

- Small/medium data set
- Faster to implement
- No DB schema changes or vector infrastructure

**Trade-offs**

- Less control over retrieval and data residency
- Higher costs at scale

**Plan**: start with File Search, and add local RAG later when scale or cost requires it.

---

## 3) OpenAI APIs to use (MVP)

1. **Text generation**: Use the existing chat completion flow but adopt **structured output** for JSON/MDX generation.

- Model: `gpt-5` (already in use).
- Use **JSON schema output** (structured output) when generating structured content blocks.
- Pass a `ChatResponseFormat` with a JSON schema to enforce output structure.

2. **File Search**: Use OpenAI Assistants API with vector store for file search.
3. **Image generation**: keep DALL·E 3 usage for illustrative media.

> **SDK note**: Upgrade `OpenAI` NuGet package from `2.3.0` to latest (`2.5.x+`) to access the Assistants/Files API surface.

> Embeddings and local RAG can be added later without changing the user-facing API.

---

## 4) Data model extensions

### 4.1 Site Profile

Add a site-level configuration table (or extend existing settings):

- `site_topic` (short summary)
- `site_audience`
- `brand_voice`
- `preferred_terms`
- `avoid_terms`
- `style_examples` (optional links or content IDs)

### 4.2 Knowledge Base (File Storage sync)

Add a new entity to track OpenAI file storage per site:

- `KnowledgeFile` (site_id, openai_file_id, last_sync_token, status, metadata)

### 4.3 Media Index (MVP)

Store **media metadata only** in OpenAI File Storage for search (no binaries):

- `url` (generated from `ScopeUid` + `Name`)
- `caption` (use `Description` field from existing Media entity)
- `media_id`
- `extension` / `mime_type`

> The existing `Media` entity does not have `Tags` or `AltText` fields. If needed, these can be added later or derived from `Description`.

Recommended format: **one JSON Lines file per site** to simplify updates and avoid OpenAI file count limits (max 10,000 files per org).

---

## 5) Retrieval and prompt assembly

### 5.1 Retrieval

For a request `(site_id, content_type, language, prompt)`:

1. **Find sample content** (existing in ContentGenerationService).
2. **Sync Knowledge Base** via the CMS sync API + OpenAI File Storage (if needed).
3. **Search knowledge** using OpenAI File Search (top 5–10 snippets).
4. **Search media metadata** using OpenAI File Search (top 3–5), then map to local media binaries by `media_id`.
5. **Pull MDX components** via `MdxComponentParserService` if format is MD/MDX.

### 5.2 Prompt composition

- **System prompt**: content type rules, formatting requirements, JSON schema, SEO constraints.
- **Context pack**: site profile, style summary, related content snippets, top knowledge chunks, and media suggestions.
- **User prompt**: the copywriter’s request + desired tone or constraints.

This can be implemented as a new **ContextBuilderService** and re-used by the existing `ContentGenerationService`.

---

## 6) Recommended implementation plan (incremental)

### Phase 1: File Search MVP with CMS sync

- Create a `KnowledgeFile` table to map `site_id` → `openai_file_id` + `openai_vector_store_id` + `last_sync_token` + status.
- Build a `KnowledgeSyncService` that:
  - calls the existing `SyncService` or content endpoint internally (add a method returning raw DTOs, not `IActionResult`)
  - creates a JSONL file from content and uploads to OpenAI File Storage on first use
  - updates/replaces the file on subsequent syncs
  - stores the new `last_sync_token`
- Build a `FileSearchService` that runs file search queries per request.

### Phase 2: Site Profile

- Add site profile fields to a settings table or a new `SiteProfile` entity.
- Admin UI: form to edit profile.
- Add profile retrieval to prompt builder.

### Phase 3: Content and media sampling (no embeddings)

- Sample top 2–3 existing content items per type for style.
- Build a **media metadata file** (JSONL) from the media table and sync it to File Storage.
- Add lightweight caching for repeated prompts.

### Phase 4: Content generation flow upgrades

- Extend [plugins/LeadCMS.Plugin.AI/Services/ContentGenerationService.cs](plugins/LeadCMS.Plugin.AI/Services/ContentGenerationService.cs) to:
  - fetch site profile
  - call `KnowledgeSyncService` then `FileSearchService` for related knowledge chunks
  - pass media suggestions
  - enforce JSON schema output for structured content blocks

### Phase 5: Quality and validation

- Add server-side JSON validation for generated content.
- Add MDX lint or parse check for MDX output.
- Track usage and feedback to improve prompts.

---

## 7) Internal implementation (no new endpoints)

The existing content generation API (`POST /api/content/ai-draft`) remains unchanged. All new logic is **internal**:

1. **On-demand sync**: When `GenerateContentAsync` is called, internally:
   - Check if `KnowledgeFile` record exists for the site
   - If not, call `SyncService` (or query content/media directly) to build JSONL and upload to OpenAI File Storage
   - If exists, use stored `last_sync_token` to fetch deltas via existing sync API and update the file
   - Store new `last_sync_token` and `openai_file_id`

2. **File search integration**: Before generating, query OpenAI File Search with the user's prompt to retrieve relevant knowledge chunks.

3. **Media metadata sync**: Same pattern — build JSONL from media table, upload/update in File Storage, search for relevant media.

4. **Extended request fields** (optional, minor DTO change):
   - Add `includeMedia`, `tone`, `length` to `ContentGenerationRequest` for finer control.

5. **Media suggestions in response** (optional):
   - Return recommended media IDs and URLs alongside generated content.

---

## 8) Current system prompts and required adjustments

### 8.1 Current prompts (in `ContentGenerationService.cs`)

**`BuildSystemPromptAsync`** (new content generation):

- Provides sample content structure (title, description, body snippet)
- Lists MDX components if available
- Shows slug patterns from existing content
- Specifies SEO length constraints
- Requests full JSON output with all fields

**`BuildEditSystemPromptAsync`** (edit with AI):

- Generic "content editor assistant" role
- Requests full JSON output with all fields replaced

### 8.2 Required adjustments for MVP

| Area                        | Current                             | Needed                                                                |
| --------------------------- | ----------------------------------- | --------------------------------------------------------------------- |
| **Context**                 | Single sample content               | Add site profile, knowledge chunks from File Search, media candidates |
| **Output format**           | Flat JSON with raw MDX/MD in `body` | Structured JSON blocks (section 9) for validation                     |
| **File Search integration** | None                                | Add retrieved snippets to context pack                                |
| **Media reuse**             | None                                | Include media candidates with `mediaId` + caption                     |
| **Correction mode**         | Returns full content                | Return JSON Patch operations (section 12)                             |

### 8.3 Example adjusted system prompt (generation)

```
You are an expert content creator for {site_topic}. Generate content that matches the site's voice and style.

SITE PROFILE:
- Topic: {site_topic}
- Audience: {site_audience}
- Voice: {brand_voice}
- Preferred terms: {preferred_terms}
- Avoid: {avoid_terms}

RELEVANT KNOWLEDGE (from file search):
{knowledge_chunks}

AVAILABLE MEDIA (use mediaId in Image components):
{media_candidates}

MDX COMPONENTS:
{component_list}

OUTPUT FORMAT:
Return a JSON object with this structure:
{
  "title": "...",
  "slug": "...",
  "description": "...",
  "blocks": [ ... ],
  "seo": { "metaTitle": "...", "metaDescription": "..." }
}

See schema for block types: markdown, component.
```

### 8.4 Example adjusted system prompt (edit/correction)

```
You are a content editor. Apply the requested changes to the provided content.

CURRENT CONTENT:
{current_content_json}

RULES:
- Return ONLY a JSON Patch array (RFC 6902) with the minimal changes needed.
- Do NOT return the full content.
- Each operation: { "op": "replace"|"add"|"remove", "path": "/blocks/2/markdown", "value": "..." }
- Make your best judgment on the user's intent. If the request is unclear, apply the most reasonable interpretation.
```

---

## 9) Prompt structure (example outline)

**System**

- Role: “Generate content in {format} with schema compliance”
- Schema (for JSON)
- MDX component list
- SEO constraints

**Context Pack**

- Site profile summary
- Related content summaries (2–3)
- Knowledge chunks (5–10, each 300–500 chars)
- Media candidates (IDs + short captions)

**User**

- Copywriter prompt
- Optional additional constraints

---

## 9) Structured content blocks (recommended for MVP)

Instead of generating raw MDX, generate a **validated JSON structure** that can be safely transformed into MDX.

### 9.1 Example structure

```
{
  "title": "How to Reduce Facility Downtime",
  "slug": "reduce-facility-downtime",
  "blocks": [
    { "type": "markdown", "markdown": "## Why downtime happens\n\nMost issues come from..." },
    {
      "type": "component",
      "name": "Callout",
      "props": { "variant": "info", "title": "Quick win" },
      "children": [
        { "type": "markdown", "markdown": "Start with a weekly checklist for..." }
      ]
    },
    {
      "type": "component",
      "name": "Image",
      "props": { "mediaId": "m_123", "alt": "Technician inspecting HVAC" }
    }
  ],
  "seo": {
    "metaTitle": "Reduce Facility Downtime",
    "metaDescription": "Practical steps to reduce downtime in large facilities."
  }
}
```

### 9.2 Rendering to MDX (server-side)

Convert to MDX by mapping:

- `markdown` blocks → markdown text
- `component` blocks → `<Component {...props}>...</Component>`

### 9.3 Validation loop

- Validate JSON schema server-side.
- If invalid, return structured error details to the model and request **corrected object** (not full text).
- This reduces hallucinations and makes outputs deterministic.

---

## 11) Edit with AI and JSON Patch correction flow

### 11.1 Problem with current approach

The current `GenerateContentEditAsync` returns full content JSON, even for small edits. This is:

- Slow (model regenerates everything)
- Error-prone (may change unrelated content)
- Expensive (high token usage)

### 11.2 JSON Patch approach (RFC 6902)

Instead of returning full content, the model returns an array of JSON Patch operations:

```json
[
  { "op": "replace", "path": "/title", "value": "Updated Title Here" },
  {
    "op": "replace",
    "path": "/blocks/1/markdown",
    "value": "## New heading\n\nUpdated paragraph..."
  },
  {
    "op": "add",
    "path": "/blocks/3",
    "value": { "type": "markdown", "markdown": "New section..." }
  },
  { "op": "remove", "path": "/blocks/4" }
]
```

### 11.3 Benefits

- **Faster**: Model only outputs the delta
- **Cheaper**: Fewer output tokens
- **Safer**: Changes are explicit and auditable
- **Correctable**: If patch is invalid, ask model to fix just the patch, not regenerate everything

### 11.4 Implementation flow

```
1. User submits edit request with prompt
2. Server sends current content JSON + prompt to AI
3. AI returns JSON Patch array
4. Server validates patch operations:
   - Valid JSON Patch syntax?
   - Paths exist in current content?
   - Values match expected types?
5. If valid: apply patch, return updated content
6. If invalid: send error details back to model, request corrected patch
7. Retry up to N times, then fail gracefully
```

### 11.5 System prompt for edit mode

```
You are a content editor. Apply the requested changes to the provided content.

CURRENT CONTENT (JSON):
{current_content_json}

USER REQUEST:
{user_prompt}

RULES:
1. Return ONLY a JSON Patch array (RFC 6902 format)
2. Use minimal operations to achieve the requested change
3. Valid operations: "add", "remove", "replace", "move", "copy"
4. Paths use JSON Pointer syntax: /title, /blocks/0/markdown, /seo/metaTitle
5. Do NOT return the full content object
6. Make your best judgment on the user's intent. Apply the most reasonable interpretation if unclear.

Example response:
[
  { "op": "replace", "path": "/title", "value": "New Title" },
  { "op": "replace", "path": "/blocks/0/markdown", "value": "Updated intro..." }
]
```

### 11.6 Correction request (on validation failure)

```
Your previous patch was invalid:

ERRORS:
- Path "/blocks/5" does not exist (array has 4 items)
- Value at "/blocks/1/markdown" must be a string

Please return a corrected JSON Patch array that fixes these issues.
```

### 11.7 Server-side implementation notes

- Use `Microsoft.AspNetCore.JsonPatch` or `JsonPatch.Net` NuGet package
- Validate paths against current content structure before applying
- Log all patches for audit trail
- Set max retry count (e.g., 3) to avoid infinite loops

---

## 12) Security, compliance, and cost

- Content synced to OpenAI File Storage is subject to OpenAI's data policies. Review before enabling for sensitive sites.
- Allow **per-site opt-out** of AI usage for compliance.
- Store all prompts and outputs for traceability and audits.
- Add a prompt limit & token budget system to control costs.
- OpenAI File Search limits: max **512 MB per file**, **10 GB per vector store**, **10,000 files per org**. Batch content into JSONL files per site to stay within limits.

---

## 13) Summary

You already have a strong AI plugin with content generation and MDX component awareness. The fastest MVP is **OpenAI File Search** + **structured JSON blocks** with a sync-based knowledge file built from your content table. For edits, use **JSON Patch** to minimize token usage and enable fast correction loops. This enables safe, validated output and quick iteration, while keeping a clean path to a future local RAG layer when scale requires it.
