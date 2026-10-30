---
name: amend-commit
description: 사용자가 "커밋 수정", "커밋 메시지 자세하게", "마지막 커밋 정리", "amend" 등을 요청할 때 사용합니다. 가장 마지막 커밋의 변경 사항을 분석하여 상세하고 구조화된 커밋 메시지로 수정합니다.
license: MIT
compatibility: opencode, claude-code, codex
metadata:
  author: Chillbok
  version: "1.0.0"
---

# 커밋 메시지 상세 수정 (amend-commit)

가장 마지막 커밋의 변경 사항을 분석하고, 자세한 커밋 메시지로 재작성하는 작업을 수행합니다.

## 수행 절차

1. `git log --oneline -5` 로 가장 마지막 커밋을 확인한다.
2. `git status` 로 작업 트리 상태와 push 여부를 확인한다.
   - `Your branch is ahead` / `git branch -vv` / `git log @{u}..HEAD` 등으로 원격 push 여부를 확인한다. 이미 push된 커밋이면 사용자에게 경고하고 명시적 동의 없이는 amend하지 않는다 (push 전 커밋만 amend 원칙).
   - 수정된 tracked 파일이 있으면 사용자와 협의 후 `git stash`로 임시 보관하고 진행 가능
   - **untracked 파일이 있는 경우**: `git stash -u`로 함께 보관 가능
3. `git show --stat HEAD` 와 `git show HEAD` 로 변경 사항을 상세히 분석한다.
   - 변경/추가된 파일 목록과 줄 수
   - 각 파일의 실제 코드 변경 내용 (새로 추가된 컴포넌트, 수정된 로직, 생성/삭제된 참조 등)
4. 커밋 메시지 스타일을 결정한다 — 다음 3단계 우선순위를 엄격히 따른다:
   - **1순위 — 명시적 커밋 규칙이 있는 경우:** 저장소 또는 사용자가 제공한 커밋 규칙을 최우선으로 준수한다. 탐색 대상: `CONTRIBUTING.md` / `.github/CONTRIBUTING.md` / `.github/commit-convention.md`, `AGENTS.md` / `CLAUDE.md` 내 커밋 컨벤션, `.commitlintrc` / `commitlint.config.{js,cjs,mjs}` / `.commitlintrc.{json,yaml}`, `package.json` 내 `commitlint`/`config.commitizen`, `git config commit.template` / `.gitmessage` 등. 사용자 프롬프트에서 직접 규칙을 제시한 경우 저장소 규칙보다 프롬프트 규칙을 우선한다. 규칙 간 상충 시 더 구체적/엄격한 규칙을 우선하고, 포맷 규칙(Conventional Commits 등)과 언어 규칙은 모두 충족하도록 작성한다.
   - **2순위 — 명시적 규칙이 없는 경우:** 기존 커밋 로그(`git log --oneline -5`, `git log --format=%B -n 10` 등)를 확인해 저장소 관례(한국어/영어, 접두사 `feat:/fix:` 등, 제목 길이, 본문 구조)를 따른다.
   - **3순위 — 기존 커밋도 없는 경우(신규 저장소/첫 커밋 등):** AI가 diff와 프로젝트 맥락을 기반으로 적절하다고 판단하는 형태로 작성한다. 이 경우 Conventional Commits를 기본으로 하되, 프로젝트 언어와 일관되게 작성한다.
   - **공통 원칙 (모든 순위에 적용):** 기존 커밋 메시지에 담긴 사용자의 의도/판단 근거/맥락(why) — 예: `기획 의도에 맞지 않고 지나치게 빠르다고 판단` — 및 후속 계획 등 의미 있는 정보는 어떤 경우에도 무시하거나 누락하지 않고, 초안 본문에 녹여 보강/보존한다.
   - **언어 규칙:** 제목과 본문은 영어로 작성한다. 이 규칙은 새 커밋과 amend 모두에 적용한다.
5. 커밋 메시지 초안을 작성한다:
   - 첫 줄: 4번에서 결정된 스타일에 맞는 한 줄 요약 (예: `feat: ...`)
   - 본문: `git show` 분석 결과를 항목별로 명확하게 나열하되, 공통 원칙의 why 맥락을 단순 기술 나열에 그치지 않고 본문에 녹여 보강한다
   - 참고/후속 작업이 있다면 별도 섹션으로 분리
   - 기존 커밋 본문에 있던 의미 있는 정보(후속 계획 등)는 보존한다.
6. 사용자에게 수정할 커밋 메시지 초안을 보여주고 동의를 구한다.
   - 사용자가 동의하면 `git commit --amend -m "..."` 으로 수정
   - 사용자가 다른 메시지를 원하면 입력을 받아 반영
7. amend 완료 후 변경된 커밋 해시와 결과를 간결하게 보고한다.

## 주의사항

- push 이전의 커밋만 amend 하는 것을 원칙으로 하되, 사용자가 명시적으로 요청한 경우에만 진행한다.
- 작업 트리에 미커밋 변경(수정된 tracked 파일)이 있으면 사용자 동의 없이 amend하지 않는다.
  - 단, **수정된 tracked 파일 또는 untracked 파일이 있는 경우** 사용자 동의 시 `git stash`(`-u`)로 임시 보관 후 amend 가능
  - amend 완료 후 `git stash pop`으로 복구
- 커밋 저자, 날짜 등 다른 정보는 변경하지 않는다.
- 어떤 우선순위 단계에서도 사용자가 제시한 맥락(why, 판단 근거, 후속 계획 등)을 무시하지 않는다.
- 명시적 커밋 규칙이 있는 경우 해당 규칙을 위반하지 않는다 (타입/스코프/제목 길이 등).
- 사용자가 명시적으로 요청한 경우가 아니라면 push, rebase는 수행하지 않는다.

## 명시적 호출 방법

- Claude Code: `/amend-commit`
- OpenCode: `/skills` 에서 `amend-commit` 선택 (또는 에이전트가 description 매칭 시 자동 로드)
- Codex: `$amend-commit`
