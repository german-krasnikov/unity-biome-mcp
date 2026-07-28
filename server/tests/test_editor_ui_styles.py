import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EDITOR = ROOT / "unity-plugin" / "Editor"

STYLE_FILES = [
    EDITOR / "MCPHub.uss",
    EDITOR / "MCPSettings.uss",
    EDITOR / "ArcadeAnim.uss",
    EDITOR / "Wizard" / "SetupWizard.uss",
    EDITOR / "Updates" / "LevelUpAnim.uss",
]

SOURCE_FILES = [
    EDITOR / "BiomeUI.cs",
    EDITOR / "BiomeToggleGroup.cs",
    EDITOR / "BiomeParticleBurst.cs",
    EDITOR / "HubCardButton.cs",
    EDITOR / "MCPHubUI.cs",
    EDITOR / "SettingsNavController.cs",
    EDITOR / "SettingsPageFactory.cs",
    EDITOR / "MCPSettingsUI.cs",
    EDITOR / "MCPSettingsCategoryGroup.cs",
    EDITOR / "MCPSettingsPermUI.cs",
    EDITOR / "PermCategoryGroup.cs",
    EDITOR / "Chat" / "View" / "ChatSettingsSection.cs",
    EDITOR / "Chat" / "View" / "BackendSettingsForm.cs",
    EDITOR / "Updates" / "UpdatesPage.cs",
    EDITOR / "Updates" / "VersionPickerPage.cs",
    EDITOR / "Updates" / "LevelUpPanel.cs",
    EDITOR / "Updates" / "LevelUpAnimator.cs",
    EDITOR / "Wizard" / "SetupWizard.cs",
    EDITOR / "Wizard" / "WizardStepAnim.cs",
    EDITOR / "Wizard" / "WizardUI.cs",
    *sorted((EDITOR / "Wizard" / "Screens").glob("*.cs")),
]

QUERY_ONLY_CLASSES = {
    "nav-slot-a",
    "nav-slot-b",
    "updates-checking",
}


def test_editor_uss_does_not_use_web_keyframes() -> None:
    for path in EDITOR.rglob("*.uss"):
        text = path.read_text(encoding="utf-8")
        assert "@keyframes" not in text, path
        assert "animation-name" not in text, path
        assert "pointer-events" not in text, path


def test_settings_ui_classes_have_uss_rules() -> None:
    selectors: set[str] = set()
    for path in STYLE_FILES:
        selectors.update(
            re.findall(r"\.([A-Za-z_][A-Za-z0-9_-]*)", path.read_text(encoding="utf-8"))
        )

    used: set[str] = set()
    for path in SOURCE_FILES:
        used.update(
            re.findall(
                r'AddToClassList\("([A-Za-z_][A-Za-z0-9_-]*)"\)',
                path.read_text(encoding="utf-8"),
            )
        )

    missing = sorted(used - selectors - QUERY_ONLY_CLASSES)
    assert not missing, f"USS rules missing for: {', '.join(missing)}"
