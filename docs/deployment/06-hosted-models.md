# Hosted (non-local) models

New installations use **Gemini 2.5 Flash-Lite** through OpenRouter for extraction, memory, Q&A, entity summaries, selection hints, and current scene. Embeddings use **nomic-embed-text** on local Ollama.

Any **OpenAI-compatible** endpoint works: OpenAI itself, OpenRouter, Groq, Together, Mistral, or a vLLM/llama.cpp server on a beefier box in your network.

![The AI models admin panel: providers, models, task routing, and prompt trimming](../assets/models.png)

## The trade you're making

Be aware of what leaves your server. Every task routed to a hosted model sends that task's prompt to the provider, and the prompt contains book excerpts as evidence. Two things stay true regardless of provider:

- The spoiler boundary is enforced before the call: evidence is filtered to the reader's position server-side, so a hosted model never sees further into the book than you have read.
- Every call lands in the LLM call log (admin → LLM Calls) with the evidence it saw, token counts, and cost, so you can audit exactly what was sent where.

For an entirely local setup, pull an Ollama chat model and select it for all six chat tasks before uploading a book.

## Setting it up

1. Open **Admin → AI models**, edit the seeded **OpenRouter** provider, and enter your API key. Storymark encrypts the key at rest.
2. The seeded model is `google/gemini-2.5-flash-lite` with **Provider default** reasoning. Its cost estimates use **$0.10 per million input tokens** and **$0.40 per million output tokens**. You can edit these rates in the model dialog. [OpenRouter model and pricing](https://openrouter.ai/google/gemini-2.5-flash-lite).
3. Pull `nomic-embed-text` in Ollama before uploading a book. To change a chat model later, use **Task routing**. Each selection takes effect on the next call.

On upgrade, an installation already routing chat through OpenRouter Luna switches all six chat tasks to Gemini once, reusing its stored provider key. If multiple Luna accounts are active, Storymark prefers the extraction account, then the account with the most Luna routes. Existing local-only installations keep their routing. The migration only changes chat routing, so existing embeddings remain usable.

## Suggested routing

Not all tasks gain equally from a stronger model. Roughly in order of payoff:

| Task | What it does | Hosted payoff |
| ---- | ------------ | ------------- |
| Extraction | Builds the memory: events, entities, relationships, per-section summaries. Background, once per book. | **Highest.** This is where small models leave timelines and graphs sparse. Almost all of your token spend lands here. |
| Memory | Folds each section into the rolling "story so far". Background. | High. Better narrative coherence in recaps and the Summary tab. |
| Q&A | The assistant's free-text answers. Interactive. | High. Small local models sometimes hedge on answerable questions. |
| Entity summary | The "who is this?" cards. Generated on demand, then cached per position. | Medium. Generation latency is visible the first time you tap a name. |
| Current scene | The Scene tab. On demand, cached. | Medium. |
| Selection hint | The fast hint on a text selection. Latency-sensitive by design. | Low. The local 1B model is the point here; a fast hosted small model also works. |
| Embedding | Vector search over chunks. | **Leave local.** Changing the embedding model re-embeds every book (the panel warns you), and `nomic-embed-text` is fast even on CPU. |

Three setups that make sense:

- **All local**. Free, private, slower extraction, sparser memory. Fine for trying Storymark out.
- **Hybrid**: hosted Extraction + Memory + Q&A, everything else local. Most of the quality for the least exposure; the latency-sensitive reader-time tasks never leave your box.
- **All hosted (except embedding)** (the default): a cheap small hosted model on every chat task, as in the screenshot. Consistent quality everywhere; reader-time features get noticeably sharper.

## Settings worth touching

- **Local prompt trimming** (bottom of the AI models panel): the Q&A prompt uses a reduced evidence set on local models to keep CPU inference fast. Hosted models always receive the full set. With a large-context hosted model on Q&A you can raise *Evidence chunks (hosted)* above the default 12 for better recall on sprawling books; expect proportionally higher per-question cost.
- **Prompts** are editable in **Admin → Prompts**. The defaults have been tested with Gemini 2.5 Flash-Lite. Startup applies revised defaults to prompts that still have their seeded values. Prompts saved by an operator keep their custom wording.
- **Restart a book with new models.** Save the routes under **Admin → AI models**, then open **Admin → Books**. Choose **Stop and reprocess** for an active book or **Reprocess** for a finished or failed book. Confirm to rebuild from the original file. Reading progress and bookmarks are retained. Previous extraction results and saved Q&A are cleared. An in-flight provider request may still be billed.

## Keeping an eye on cost

Extraction dominates: every section of a book goes through the model once at upload (or reprocess). Q&A costs per question; entity summaries, scene cards, and recaps are generated once per reading position and then served from cache. The admin cost meter totals it all from the per-model rates you entered, and the call log shows the spend per call when you want to know where it went.
