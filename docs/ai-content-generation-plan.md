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

1. **Text generation**: Use the existing chat completion flow and keep **raw MDX/MD** in the `body` field.

- Model: `gpt-5` (already in use).
- Keep output as flat JSON with `body` as MDX/MD string.

2. **File Search**: Use OpenAI Responses API with `file_search` tool and vector stores for knowledge retrieval.

- Create a single vector store (generate `site_id` on first use and reuse it)
- Upload JSONL files with metadata (`type`, `language`) for filtering
- Use `max_num_results` to control retrieved snippets (e.g., 5–10 for knowledge, 3–5 for media)
- Parse file citations from responses for traceability

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

Add a new entity to track OpenAI file storage:

- `KnowledgeFile` (site_id, openai_file_id, openai_vector_store_id, last_sync_token, status, metadata)

> `site_id` is generated on first use and reused for all subsequent syncs (single-site CMS)

### 4.3 Media Index (MVP)

Store **media metadata only** in OpenAI File Storage for search (no binaries):

- `url` (generated from `ScopeUid` + `Name`)
- `caption` (use `Description` field from existing Media entity)
- `media_id`
- `extension` / `mime_type`

> The existing `Media` entity does not have `Tags` or `AltText` fields. If needed, these can be added later or derived from `Description`.

Recommended format: **one JSON Lines file** to simplify updates and avoid OpenAI file count limits (max 10,000 files per org).

---

## 5) Retrieval and prompt assembly

### 5.1 Retrieval

For a request `(content_type, language, prompt)`:

1. **Find sample content** (existing in ContentGenerationService).
2. **Sync Knowledge Base** via the CMS sync API + OpenAI File Storage (if needed).
3. **Search knowledge** using OpenAI File Search (top 5–10 snippets).
4. **Search media metadata** using OpenAI File Search (top 3–5), then map to local media binaries by `media_id`.
5. **Pull MDX components** via `MdxComponentParserService` if format is MD/MDX.

### 5.2 Prompt composition

- **System prompt**: content type rules, formatting requirements, SEO constraints.
- **Context pack**: site profile, style summary, related content snippets, top knowledge chunks, and media suggestions.
- **User prompt**: the copywriter’s request + desired tone or constraints.

This can be implemented as a new **ContextBuilderService** and re-used by the existing `ContentGenerationService`.

---

## 6) Recommended implementation plan (incremental)

### Phase 1: File Search MVP with CMS sync

- Create a `KnowledgeFile` table with a single record: `site_id` (GUID), `openai_file_id`, `openai_vector_store_id`, `last_sync_token`, `status`.
- Build a `KnowledgeSyncService` that:
  - calls the existing `SyncService` or content endpoint internally (add a method returning raw DTOs, not `IActionResult`)
  - on first use: generates `site_id` (GUID), creates JSONL file from content with metadata (`type`, `language`), uploads to OpenAI File Storage, creates vector store, saves record
  - updates/replaces the file on subsequent syncs using `last_sync_token`
  - stores the new `last_sync_token` and checks file processing status before search
- Build a `FileSearchService` that:
  - calls Responses API with `file_search` tool, passing `vector_store_ids`
  - sets `max_num_results` to cap snippets (e.g., 5–10 knowledge, 3–5 media)
  - parses file citations from responses for audit/trace

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

### Phase 5: Quality and validation

- Add MDX lint or parse check for MDX output.
- Track usage and feedback to improve prompts.

---

## 7) Internal implementation (no new endpoints)

The existing content generation API (`POST /api/content/ai-draft`) remains unchanged. All new logic is **internal**:

1. **On-demand sync**: When `GenerateContentAsync` is called, internally:
   - Check if `KnowledgeFile` record exists (single record for entire CMS)
   - If not, generate a `site_id` (e.g., GUID), build JSONL, upload to OpenAI File Storage, create vector store
   - If exists, use stored `last_sync_token` to fetch deltas via existing sync API and update the file
   - Store new `last_sync_token`, `openai_file_id`, and `openai_vector_store_id`

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
| **Output format**           | Flat JSON with raw MDX/MD in `body` | Keep as-is (raw MDX/MD in `body`)                                     |
| **File Search integration** | None                                | Add retrieved snippets to context pack                                |
| **Media reuse**             | None                                | Include media candidates with `mediaId` + caption                     |
| **Correction mode**         | Returns full content                | Keep as-is (full content)                                             |

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
  "description": "...",
  "slug": "...",
  "type": "...",
  "author": "...",
  "language": "...",
  "category": "...",
  "tags": [...],
  "allowComments": true/false,
  "coverImageUrl": "...",
  "coverImageAlt": "...",
  "body": "<MDX or Markdown string>"
}
```

### 8.4 Example adjusted system prompt (edit/correction)

```
You are a content editor. Apply the requested changes to the provided content.

CURRENT CONTENT:
{current_content_json}

RULES:
- Return the full updated content JSON.
- Keep body as raw MDX/Markdown in the "body" field.
- Make your best judgment on the user's intent. If the request is unclear, apply the most reasonable interpretation.
```

## 10) Security, compliance, and cost

]

```

### 11.6 Correction request (on validation failure)

```

Your previous patch was invalid:

ERRORS:

- Path "/body/5" does not exist (array has 4 items)
- Value at "/body/1/markdown" must be a string

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

## 11) Summary

You already have a strong AI plugin with content generation and MDX component awareness. The fastest MVP is **OpenAI File Search** with a sync-based knowledge file built from your content table. Content generation keeps **raw MDX/MD in the body field**, and edits return the **full updated content**. This keeps output quality high and minimizes latency while preserving a path to a future local RAG layer when scale requires it.
```
