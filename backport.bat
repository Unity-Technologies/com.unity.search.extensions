@ECHO OFF

SET UNITY_DIR="U:/master"
SET PACKAGE_DIR="%CD%/package"

REM Define Beyond Compare executable path
SET BC_CMD="C:/Program Files/Beyond Compare 4/BComp.com"

REM Compare editor sources
%BC_CMD% /iu "%UNITY_DIR%/Modules/QuickSearch/Editor/Dependencies" "%PACKAGE_DIR%/Dependencies" /filters="-*.meta"

REM Compare tests
%BC_CMD% /iu "%UNITY_DIR%/Modules/QuickSearch/Tests/QuickSearch/Assets/Dependencies" "%CD%/projects/TestDependencies/Assets/Dependencies" /filters=""
%BC_CMD% /iu "%UNITY_DIR%/Modules/QuickSearch/Tests/QuickSearch/Assets/Editor/Tests/Dependencies" "%CD%/projects/TestDependencies/Assets/Editor" /filters="-TestUtils.*;-com.unity.search.extensions.tests.asmdef*"
