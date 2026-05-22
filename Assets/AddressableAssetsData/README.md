# Addressables 세팅 안내

이 프로젝트(LOP-Server)는 Addressables를 **원격 S3 catalog 기반**으로 사용합니다.

## 핵심 전제

- **서버는 에셋 publisher가 아닙니다.** Knight/Archer/Necromancer/FlapWangMap 등 실제 prefab/scene은 클라이언트 프로젝트에서 빌드되어 S3에 업로드됩니다.
- 따라서 **`Default Local Group`이 비어 있는 것이 정상**입니다. 여기에 에셋을 추가하지 마세요.
- 서버는 런타임에 S3 catalog를 다운로드해서 그 안의 entry를 그대로 사용합니다.

## 프로필 구성

| Profile | Remote.LoadPath |
|---|---|
| Default | `<undefined>` ← 이 프로필이 활성이면 빌드 실패 |
| dev | `https://lop-assets.s3.ap-northeast-2.amazonaws.com/dev/[BuildTarget]` |
| stage | `https://lop-stage-assets.s3.ap-northeast-2.amazonaws.com/[BuildTarget]` |
| prod | `https://lop-prod-assets.s3.ap-northeast-2.amazonaws.com/[BuildTarget]` |

## 에디터 Play Mode 동작시키기

처음 머신을 세팅하거나 `Library` 폴더가 삭제된 직후에는 아래 3가지를 모두 확인해야 합니다.

### 1. Active Profile을 dev(또는 stage/prod)로 (머신별 1회)

`Window → Asset Management → Addressables → Profiles` 창에서 **dev** 우클릭 → **Set Active**

> 활성 프로필은 `Library/AddressablesConfig.dat`(per-user, gitignored)에 저장되므로 머신마다 따로 설정해야 합니다. asset 파일의 `m_ActiveProfileId`는 default 값일 뿐, 이 .dat가 항상 우선합니다.

### 2. Addressables Build 한 번 실행 (머신별 1회)

`Window → Asset Management → Addressables → Groups` 창 상단의 **Build → New Build → Default Build Script**

> 이 빌드가 `Library/com.unity.addressables/aa/[BuildTarget]/settings.json`을 생성하며, 그 안에 S3 catalog URL이 baked-in 됩니다. 에디터 Play Mode가 부트스트랩할 때 이 파일을 읽어 S3에서 catalog를 다운로드합니다.

### 3. Play Mode Script를 Use Existing Build로 (세션별 확인)

`Groups` 창 상단 드롭다운에서 **Use Existing Build**.

> **Use Asset Database를 절대 쓰지 마세요.** AssetDatabase 모드는 로컬 그룹의 엔트리만 보기 때문에, 빈 그룹인 본 프로젝트에서는 모든 `Addressables.LoadAssetAsync(...)` 호출이 `InvalidKeyException`으로 실패합니다.

## 자주 보는 에러 → 원인

| 에러 메시지 | 원인 | 해결 |
|---|---|---|
| `Remote Build and/or Load paths are not set ... '<undefined>'` (빌드 시) | Active profile이 Default | 위 1번 |
| `InvalidKeyException: No Location found for Key=Assets/Art/...` (Play Mode) | Play Mode Script가 Use Asset Database이거나, 로컬 `aa/` 빌드가 없음 | 위 2번 + 3번 |
| `ArgumentException: The Object you want to instantiate is null` | 위 InvalidKeyException의 연쇄 결과 | 위와 동일 |

## 무시해도 되는 노이즈 (서버 단독 구동 시)

- `Curl error 7: Failed to connect to localhost port 1340` — 로컬 로비/유저 서버 미기동
- `http://room-server-service/... Cannot resolve destination host` — Kubernetes 서비스 이름이라 에디터에서는 resolve 불가
- `[Licensing::Client] Error: Code 10` — Unity Hub 라이선스 검증 경고, 무해
- `com.unity.ai.assistant` 호환성 경고 — Unity 6000.0.36f1 미지원
