# VitaForge

RomForge에서 PS Vita 기능만 분리한 프로젝트입니다.

- PS Vita 리팩: PKG / NoNpDrm / zip / 폴더 원본을 언팩·리팩하고 xdelta 패치를 적용합니다.
- PS Vita 포맷 변환: PKG를 NoNpDrm 형식으로 변환합니다.

## 구성

- VitaForge: WPF 앱
- VitaForge.Core: 앱 공통 코드 (설정, 뷰모델 기반, 사운드, 업데이트 확인, Vita 서비스)
- Vita: PS Vita 코어 라이브러리
- Patch: 패치 코어 라이브러리
- Common: 공통 라이브러리 (다국어 포함)

## 빌드

BUILD.bat 실행
