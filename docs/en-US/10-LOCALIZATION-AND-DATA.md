# 10 Localization, Numeric Display, and Data Semantics

Chinese and English are first-class and must cover Menu, Tool, ToolPanel, Property, enum values, prompts, errors, status, snap names, grip results, and file messages. Fallback text is a development safety net, not a permanent English leak.

Internal IDs, Action IDs, EntityType values, and serialization field names remain stable English identifiers and never change with UI language.

User-facing enums such as line style, display mode, material, and snap mode use a single localized display mapping rather than raw `ToString()`.

Engineering doubles display `0.000` by default. Formatting never rounds model state, history snapshots, or serialized values. For example, 12.345678901 displays as 12.346 but remains 12.345678901 unless the user edits it. Numeric parsing accepts current culture and invariant decimal input, rejects empty/NaN/Infinity/out-of-range values locally, and does not create duplicate history entries through Enter plus LostFocus.

Units belong to display/parse conversion; entities store canonical internal values.

Appearance uses independent state pairs: ColorByLayer + Color, LineWidthByLayer + LineWidth, LineStyleByLayer + LineStyle. Turning ByLayer on does not need to destroy the stored override value; turning it off can reuse the prior override.

Layer and entity color buttons open ColorDialog directly. Preview and final Document presentation use the same appearance resolver, including current-layer resolution before a new entity has been committed.

Serialization writes stable identifiers, round-trip numeric precision, and language-independent enum/type values. Compatibility logic stays at the serializer boundary rather than leaking into public entity APIs or UI.
