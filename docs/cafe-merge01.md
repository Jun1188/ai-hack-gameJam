# AutumnCafe + test-serv integration

Open `ai-hack-test/Assets/Scenes/test-loop-merge01.unity` in Unity 6000.6.8f1.
The scene combines AutumnCafe at `2c53b5e7` with test-serv ordering,
conversation, menu service and customer ratings. Both source scenes are preserved.

## Controls and UI

- WASD / mouse: AutumnCafe movement and view; F: existing drink preparation.
- Tab: compact right sidebar. Order, menu, conversation and settings pages each
  have one vertical scroll area. The complete session conversation stays in memory.
- E: toggle customer chat input; Enter: send; Esc: close overlays and release cursor.
  Tab can reopen the sidebar after Esc. Overlay controls block movement and looking.
- M: original drink reference cards.
- Customer messages appear at the upper left for six seconds; the E hint stays
  at the lower right. Ratings retain their display time before a following order.
- Inactive customers form a line. Only one customer orders at a time. The active
  order has an exact 120-second deadline; expiration gives zero stars and admits
  the next waiting customer. Only the ordering customer can chat.

## Gemini configuration

The merged scene uses Gemini's native `generateContent` API and
`gemini-3.5-flash-lite`. See the [official API reference](https://ai.google.dev/api/generate-content).
The earlier model returned HTTP 404 with a model retirement/access restriction.

Credentials are never serialized into the scene. In the Editor, set
`GEMINI_API_KEY`, or use the ignored local file
`ai-hack-test/UserSettings/CafeChatApiKey.txt`. The settings page accepts a masked
key for the current run and allows changing the model and restarting the connection.
Players use the environment variable or the current-run settings field.

## Verification (2026-11-06)

- Unity compilation passed; merged scene has no missing MonoBehaviour scripts.
- A real Gemini reply was received and parsed in Play Mode after changing model
  and key. The latest sidebar and input changes do not alter the response contract.
- Play Mode screenshots checked sidebar alignment, settings visibility and the
  final menu items at the end of the scroll area.
- Programmatic Play Mode checks exercised Tab -> Esc -> Tab state transitions
  and mouse-delta injection while the sidebar was open; the camera stayed still.
  These checks do not replace physical keyboard or Korean IME typing verification.
- Queue checks covered just before/after 120 seconds, zero-star publication,
  promotion of the next customer and rejection of a second simultaneous order.
- The original test-serv scene retains its default queue behavior. Packages and
  project settings were not intentionally changed. No player build was generated.
