# Ubiquitous Language

The shared vocabulary of the LiveMarkDownEditor domain. These terms are **authoritative**: every
type, method, namespace, variable, and test name must use them exactly. Do not introduce synonyms.
To add a concept, define it here first, then use it in code.

> Status: living document. Add terms as the domain grows.

## Terms

| Term | Definition |
| --- | --- |
| **Markdown Document** | The source text authored by the user, written in Markdown syntax. The primary aggregate of the editor. |
| **Render** | The act of transforming a Markdown Document's source text into its HTML representation. |
| **Rendered Output** | The HTML produced by rendering a Markdown Document. |
| **Live Preview** | The continuously updated Rendered Output shown alongside the source as the user edits. |
| **Editor Session** | An active editing context holding the current Markdown Document and its Live Preview. |

<!--
Add new terms above. Each term should have:
- a single, unambiguous definition
- no overlap or synonym with an existing term
When a term changes meaning, update every usage in code and tests in the same change.
-->
