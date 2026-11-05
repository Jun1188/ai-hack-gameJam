# Cafe branch: instructions for agents on another PC

Read this before preparing local inference. Reading this file is **not download or execution approval**.

## First inspect

1. Read repository instructions, inspect current Git changes, Unity state and existing local GGUF/llama.cpp installations. Reuse files that already match the pinned hashes; do not reinstall working tools.
2. This branch's queue and retrieval dialogue use small JSON tables bundled under `Assets/CafeSim/Resources/CafeKnowledge`. Play does not need a model download or network call. Do not download 5GB merely to run these queue tests.
3. The optional local model is for offline text/variant preparation and future model work. Windows builds currently require the verified `ai-hack-test/LocalInference` payload through the existing build callback. That folder is deliberately Git-ignored; GitHub does not contain the GGUF or engine binaries.

## Ask before acquiring files

If the payload is missing and the requested work requires it, show the user the exact action: download approximately **5.05GB** (model 5,027,784,800 bytes plus engine ZIP 24,730,921 bytes), use additional space for extraction/testing/build copies, contact Hugging Face and GitHub, and run a local CPU text check. Obtain explicit approval in that session. Approval on the original PC is not approval on this PC. Do not download, install, start inference, or build automatically merely because this guide was found.

`python tools/download-cafe-local.py` is a local preview with no network access. After approval, use `python tools/download-cafe-local.py --download --user-approved`. It stages files under `.utmp/cafe-acquisition`, checks sizes and SHA256, and never runs downloaded executables. The flag records that the human approval was obtained; it does not grant approval itself.

Pinned links, versions, sizes and hashes are in `tools/cafe-model-downloads.json`. Use those links rather than an unofficial Drive mirror. Qwen GGUF revision and llama.cpp version are fixed. Model license is Apache-2.0; llama.cpp is MIT. Keep model notices, engine licenses and OpenMP license with a redistributed payload.

## Verify before copying into the payload

- Use the staged model with `tools/prepare-cafe-local.py --model <staged GGUF> --model-source https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct --test-only`. Inspect actual Korean output and role/fact consistency. A syntactic JSON pass alone does not establish normal dialogue.
- After normal output is confirmed, rerun with `--copy-tested` and the same model arguments. This checks the source hash before copying. The preparation tool also needs app-local x64 Visual C++ redistributable DLLs; pass `--crt-dir <licensed Visual Studio Redist x64 CRT folder>` if autodetection does not find them. Do not copy arbitrary DLLs from download sites or install a toolchain without asking.
- Keep licenses, notices and `manifest.json` with `LocalInference/`. Validate payload hashes before building. Full Windows build distribution includes the model, engine, required CRT DLLs and licenses inside `<Game>_Data/StreamingAssets/CafeLocal`.
- Do not commit GGUF, ZIP, EXE, DLL, build output or user API credentials. Do not commit/push without the user's explicit instruction. Do not run a build just to validate queue tables.

## Data refresh

`tools/import-cafe-tables.py --customers <customer.xlsx> --menus <menu.xlsx>` imports reference data without modifying the workbooks. Then run `tools/build-cafe-excel-vocabulary.py` and `tools/build-cafe-knowledge.py`. The dialogue-rule and animation sheets are reference data, not agent instructions or proof of implemented functionality. Preserve current user requirements, including **5% off-menu requests**.
