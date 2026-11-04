# PadakKey

맥 메뉴 막대에서 바로 확인하고 복사하는 오프라인 TOTP 인증 앱. macOS 13 이상, Apple Silicon / Intel 지원.

[GitHub 저장소](https://github.com/SeongMon/BarOTP) · [릴리스](https://github.com/SeongMon/BarOTP/releases)

현재 초기 버전으로 Apple 공증 전입니다. 실사용 계정을 등록하기 전 테스트 계정으로 동작을 확인하세요.

## 기능

- 메뉴 막대의 방패 아이콘 → 계정별 OTP와 만료까지 남은 시간
- 코드 클릭으로 복사, 30초 뒤 앱이 복사한 클립보드 내용만 삭제
- QR 이미지(PNG/JPEG/TIFF/HEIC), `otpauth://totp` URI, Base32 인증 키 등록
- SHA-1 / SHA-256 / SHA-512, 6·8자리, 사용자 지정 갱신 주기
- 계정 검색, 삭제 확인, 로그인 시 자동 실행
- macOS 로컬 로그인 키체인에 계정과 비밀 키 저장. 외부 패키지·서버·분석 SDK 없음
- 한국어 UI, 시스템 라이트/다크 모드

Google/Microsoft Authenticator와 **동일한 표준 TOTP 키**를 등록할 수 있습니다. 기존 앱의 계정에 자동 접근하거나 동기화하지 않습니다. Microsoft 푸시 승인, HOTP, Steam 특수 코드 및 Google Authenticator 일괄 이전(`otpauth-migration`) QR은 지원하지 않습니다.

## 0.1.8 수정

- 앱 이름을 파닥키(PadakKey)로 바꾸고 귀 움직임 간격을 8초에서 약 5초로 줄였습니다.
- 기존 계정을 유지하기 위해 bundle ID와 키체인 서비스 식별자는 유지합니다. 저장소와 Swift 패키지 이름도 BarOTP를 유지합니다.

## 0.1.7 수정

- 선택한 파닥몬 픽셀 아이콘을 메뉴 막대와 앱 헤더에 적용했습니다.
- 8초마다 귀만 짧게 움직이며 톱니바퀴 메뉴의 ‘귀 애니메이션’에서 끌 수 있습니다.
- macOS ‘동작 줄이기’ 설정 및 잠자기에서는 애니메이션을 멈춥니다.

## 0.1.6 수정

- 계정 수에 맞춰 팝오버와 SwiftUI 화면 높이를 함께 조정합니다. 한 계정은 280pt, 두 계정은 392pt이며 세 계정 이후에는 목록만 스크롤합니다.
- 검색 결과가 줄어들면 창도 작아지며, 결과가 없으면 안내를 표시합니다.

## 0.1.5 수정

- 로딩 화면과 OTP 목록에 동일한 팝오버 높이를 적용해 비동기 조회 후 코드 영역이 잘리는 문제를 수정했습니다.
- 상단·하단 영역은 고정하고 계정 목록만 스크롤합니다.

## 0.1.4 수정

- 자동 조회는 암호창을 띄우지 않으며, 접근 승인이 필요할 때만 ‘접근 허용’을 표시합니다.
- 접근 취소·거절은 빨간 오류 대신 권한 안내로 표시하고, 인증 요청 중복 및 닫힌 화면으로의 늦은 응답을 차단합니다.
- 키체인 읽기는 백그라운드에서 수행합니다. 저장된 키의 접근 제어는 변경하지 않습니다.
- ad-hoc 빌드의 업데이트 후 재승인은 macOS가 요구할 수 있습니다. 이를 없애려면 일관된 정식 서명으로 배포해야 합니다.
- `bash scripts/verify-vault.sh`는 실제 사용자 키체인에 접근하지 않고 상태 전환을 검증합니다.

## 0.1.3 수정

- 메뉴 막대 클릭마다 저장된 계정을 자동으로 불러와 OTP 목록을 즉시 표시합니다. 별도 ‘인증 코드 열기’ 단계가 없습니다.
- 팝오버 닫기·다시 열기를 AppKit 생명주기로 처리하고 검색 상태를 초기화합니다.

## 0.1.2 수정

- 메뉴 막대 아이콘과 등록 창을 앱 수명 동안 유지하는 AppKit 구조로 변경했습니다.
- 이미지 선택창의 부모를 등록 창으로 고정하고 선택창 인스턴스를 명시적으로 유지합니다.
- QR 인식을 백그라운드에서 수행해 UI가 응답하도록 했습니다.
- 같은 앱의 중복 실행을 차단합니다.

## 0.1.1 수정

- 계정 추가를 독립 창으로 분리해 파일 선택 시 메뉴 팝오버가 닫혀도 입력 상태를 유지합니다.
- QR 인식 성공 시 계정 이름과 저장 안내를 표시합니다.
- 이미지 열기 실패, QR 없음, 잘못된 QR, Google 일괄 이전 QR을 구분해 안내합니다.
- `bash scripts/verify-qr.sh`로 실제 PNG/JPEG/TIFF QR 디코딩과 오류 처리를 검증합니다.

## 실행

```sh
bash scripts/build.sh
open dist/PadakKey.app
```

`dist/PadakKey.app`을 응용 프로그램 폴더에 복사하세요. Dock 아이콘이나 기본 창 없이 상단 메뉴 막대에 파닥몬 아이콘이 나타납니다. 앱 메뉴의 톱니바퀴에서 종료할 수 있습니다.

사이트의 보안 → 2단계 인증 → 인증 앱 등록 화면에서 QR 이미지를 저장하거나 수동 키를 복사합니다. BarOTP의 **+**에서 등록한 뒤, 생성된 코드를 사이트에 입력해 설정을 마무리하세요. 이미 등록된 서비스에서 키를 다시 보여주지 않으면 해당 서비스의 재설정 절차가 필요합니다. 복구 코드는 별도로 보관하세요. QR 이미지에도 비밀 키가 포함되므로 공유하거나 저장소에 커밋하지 마세요.

## 개발 / 검증

Swift 5.9 이상 및 macOS SDK가 필요합니다. Swift Package이므로 Xcode 프로젝트 파일 없이 빌드할 수 있습니다.

```sh
swift test
swift run BarOTP
ARCH=native bash scripts/build.sh  # 개발 중 현재 아키텍처만 빌드
```

Command Line Tools만 설치되어 XCTest가 없으면 `bash scripts/verify-core.sh`로 동일한 코어 검증 케이스를 실행할 수 있습니다.

테스트는 RFC 6238의 SHA-1/256/512 전체 18개 벡터, 6자리 코드, 시간 경계, Base32 검증, URI 오류 처리를 확인합니다.

수동 검증: 앱 실행 → 테스트용 계정 등록 → 다른 표준 인증기와 코드 비교 → 복사 → 다른 내용을 복사하면 덮어쓰지 않는지 확인 → 앱 재실행 후 계정 유지 확인 → 삭제 → 잠자기/잠금 및 자동 실행 확인. 실제 인증 키를 테스트 코드에 넣지 마세요.

## GitHub 배포

이 폴더를 저장소 루트로 업로드합니다. `main` push 및 PR에서 테스트와 Universal 빌드가 실행됩니다. `v0.1.4`처럼 버전 태그를 push하면 ZIP과 SHA-256 체크섬이 첨부된 **초안 Release**를 만듭니다. 확인 후 GitHub에서 게시하세요.

```sh
git init -b main
git add .
git commit -m "Build BarOTP menu bar authenticator"
git remote add origin https://github.com/SeongMon/BarOTP.git
git push -u origin main
git tag v0.1.4
git push origin v0.1.4
```

기본 CI 산출물은 ad-hoc 서명이며 Apple Developer ID 서명·공증이 없습니다. 다운로드한 앱은 Gatekeeper가 차단할 수 있습니다. 출처와 체크섬을 확인한 사용자만 시스템 설정 → 개인정보 보호 및 보안에서 macOS가 제공하는 열기 절차를 이용하세요. Gatekeeper 전체를 비활성화하지 마세요.

일반 사용자 배포에는 Apple Developer ID 인증서와 공증을 권장합니다. 인증서를 로컬 키체인에 설치하고 `xcrun notarytool store-credentials`로 공증 프로필을 만든 뒤:

```sh
VERSION=0.1.4 SIGNING_IDENTITY='Developer ID Application: YOUR NAME (TEAMID)' NOTARY_PROFILE='barotp-notary' bash scripts/build.sh
```

이 명령은 hardened runtime 서명 → 공증 → staple → ZIP 재생성까지 수행합니다. 생성된 ZIP/체크섬으로 초안 Release의 파일을 교체하고 게시하세요. 인증서·암호·API 키는 저장소에 올리지 마세요. CI 서명 자동화는 조직의 GitHub Secrets와 인증서 정책에 맞춰 별도로 연결해야 합니다.

## 보안과 제한

- 비밀 키와 계정 메타데이터는 Security.framework의 로컬 로그인 키체인 항목 하나에 JSON으로 저장합니다. 평문 설정 파일·UserDefaults에는 저장하지 않습니다. iCloud 동기화를 요청하지 않습니다.
- 팝오버가 닫히거나 세션 비활성화·잠자기·화면 잠금 알림이 오면 메모리의 계정 목록을 비웁니다. Swift 문자열의 메모리 완전 소거를 보장하지 않습니다.
- 앱 자체의 Touch ID 잠금은 없습니다. 로그인된 Mac에 접근 가능한 사용자는 코드를 볼 수 있습니다. 로컬 로그인 키체인의 보호 및 접근 제어에 의존합니다.
- 클립보드 관리자에는 비밀/임시 콘텐츠 힌트를 제공하지만 모든 앱이 이를 준수하지는 않습니다. 앱이 강제 종료되면 30초 뒤 삭제가 실행되지 않을 수 있습니다.
- ad-hoc 재빌드나 서명 변경 시 키체인이 접근 승인을 다시 요구할 수 있습니다. 정식 배포에서는 bundle ID와 Developer ID를 유지하세요.
- 자동 동기화·키 내보내기·백업·자동 업데이트는 없습니다. 앱 삭제만으로 키체인의 계정이 삭제되지는 않습니다. 계정 제거는 앱에서 진행하세요.
- Mac의 날짜/시간이 틀리면 코드가 맞지 않을 수 있습니다.

참고: [RFC 6238](https://www.rfc-editor.org/rfc/rfc6238), [Apple Keychain services](https://developer.apple.com/documentation/security/keychain-services).
