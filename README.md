# CSOC
 Coastal Surveillance and Mobile Battalion

## 범용 수동 기록장 프로토타입 — 설정 UI·사진 파일명 수정본

현재 수동 기록장 소스를 보존한 버전입니다. C# 5와 Windows Forms를 사용하며,
외부 라이브러리 없이 Windows의 `csc.exe`로 빌드합니다.

- [사용설명서](Prototype/사용설명서.md)
- [전체 코드 인쇄용 TXT](Prototype/전체코드_인쇄용.txt)
- [전체 코드 인쇄용 HTML](Prototype/전체코드_인쇄용.html)
- [소스 ZIP](Prototype/COAST_Prototype_Source.zip)
- [VBS 빌드 스크립트](Prototype/Build.vbs)

`Program.cs`, `Core.cs`, `Dialogs.cs`, `MainForm.cs`, `Build.vbs`를 같은 폴더에
저장하고 `Build.vbs`를 실행합니다. CS 파일은 UTF-8로 저장하세요.

2026-11-03 빌드 스크립트 수정본은 `cmd.exe`를 거치지 않고
`WScript.Shell.Exec`로 컴파일러를 직접 실행합니다.
Windows PC에서 이 수정본의 실행 성공 여부는 아직 확인하지 못했습니다.

기록·CSV·사진 파일 관리, 일반 일정과 도움말 기능을 포함합니다.
실시간 영상 처리, 대상 추적, 카메라/레이더 연동, 클릭 매크로는 포함하지 않습니다.
수정 이력과 휴지통은 없으며, 기록 삭제 시 연결된 사진 파일은 남깁니다.

Linux Mono에서 C# 5 문법 컴파일, 핵심 데이터 시험 27개, 별도 CSV 파서 검증 1개,
가상 화면 UI 시험 21개가 통과했습니다. 실제 Windows의 컴파일러, VBS, 한셀 연결과
전체 파일 처리 흐름은 가상 자료로 추가 확인해야 합니다.

실행 시 생성되는 `Prototype/Data/`, `_build/`, `Build.log`와 EXE는 Git에서 제외합니다.
설정에서 별도로 지정한 출력 폴더의 실제 데이터도 저장소에 추가하지 마세요.

2026-11-06 수정 범위는 [변경사항](Prototype/변경사항_2026-11-06.md)을 참고하세요.
