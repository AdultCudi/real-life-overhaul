using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// Giving a role of a business its uniform preset on the RLO interface. The game's own Settings page holds the window that does it
// (SetUpUniformsWindow: a role list and a preset list, whose second list writes BuildingRegistration.uniformsBySkill and tells the
// staff to dress); the kit's page had only the link to the editor, so a preset could be made and never given to anyone. Also the
// editor's name field: the game names a preset when the field's editing ends, which on the kit has to be said on each word typed.
// Read from the sources and the laid page, as the game's windows are UI the checker cannot run.
internal static partial class Program
{
    private static void BizManUniformsAll()
    {
        // The page lays both lists with their labels, and each opens its own list of options over the page.
        BizManModel m = UiSamples.BizMan(UiWords("en"), "store_settings");
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "BizMan");
        var s = new UiScene();
        BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        Check(m.UniformRoles.Length == 2 && m.UniformSets.Length == 3 && s.FindNode(BizManPages.UniformRole, 0) >= 0 && s.FindNode(BizManPages.UniformSet, 0) >= 0
              && s.FindNode(BizManPages.Uniforms, 0) >= 0 && s.FindNode(BizManPages.UniformRoleOption, 0) < 0 && s.FindNode(BizManPages.UniformSetOption, 0) < 0,
            "bizman uniforms: the settings page has the role and the preset lists beside the editor's link, their options shut until pressed");
        m.UniformSetOpen = true;
        s = new UiScene();
        BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.UniformSetOption, 0) >= 0 && s.FindNode(BizManPages.UniformSetOption, 2) >= 0 && s.FindNode(BizManPages.UniformRoleOption, 0) < 0,
            "bizman uniforms: the preset list opens with Unassigned and every preset, the role list stays shut");
        m.UniformSetOpen = false;
        m.UniformRoleOpen = true;
        s = new UiScene();
        BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.UniformRoleOption, 1) >= 0 && s.FindNode(BizManPages.UniformSetOption, 0) < 0,
            "bizman uniforms: the role list opens with every role of the business");

        // A business with no uniform locker has no lists and says why, as the game's page does.
        m = UiSamples.BizMan(UiWords("en"), "store_settings");
        m.UniformRoles = m.UniformSets = new string[0];
        m.UniformsNote = UiWords("en")("bizman_settings_need_uniform_locker", "Uniform Locker required");
        m.Uniforms = null;
        s = new UiScene();
        BizManPages.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.UniformRole, 0) < 0 && s.FindNode(BizManPages.UniformSet, 0) < 0 && s.FindNode(BizManPages.Uniforms, 0) < 0,
            "bizman uniforms: a business without a uniform locker shows no lists and no link, only the game's line");

        // The commands are known to the checks' list, none twice.
        int[] ours = { BizManPages.UniformRole, BizManPages.UniformRoleOption, BizManPages.UniformSet, BizManPages.UniformSetOption };
        bool known = true, apart = true;
        foreach (int c in ours)
        {
            known &= Array.IndexOf(BizManPages.Commands, c) >= 0;
            apart &= Array.IndexOf(BizManPages.Commands, c) == Array.LastIndexOf(BizManPages.Commands, c);
        }
        Check(known && apart && new HashSet<int>(ours).Count == ours.Length, "bizman uniforms: the four commands are listed once each");

        // The presses go to the game's own two lists: an option is the game's SelectOption, so its listeners give the role the preset.
        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs");
        Check(press.Contains("case BizManPages.UniformRoleOption:") && press.Contains("Pick(UniformList(b, GameContract.UniformRole), arg)")
              && press.Contains("case BizManPages.UniformSetOption:") && press.Contains("Pick(UniformList(b, GameContract.UniformSet), arg)")
              && press.Contains("Get<SetUpUniformsWindow>(GameContract.SetUniforms, b.bizManSettings)"),
            "bizman uniforms: choosing a role or a preset is the game's own drop-down choice, so the game gives the role the preset and dresses the staff");
        Check(MemberText(press, "private static bool Pick(").Contains("d.SelectOption(option);"), "bizman uniforms: the choice goes through Dropdown.SelectOption, which tells the game's listeners");

        // Read from the game's window as the game filled it, shown only where the game shows it.
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs");
        string read = MemberText(parts, "private void Uniforms(BizManSettings set)");
        Check(read.Contains("GameContract.UniformWindowOn") && read.Contains("GameContract.UniformWindowOff") && read.Contains("on.activeInHierarchy")
              && read.Contains("bizman_settings_need_uniform_locker") && read.Contains("Options(role)") && read.Contains("Options(preset)") && read.Contains("SelectedOptionIndex"),
            "bizman uniforms: the lists are the game's own window's, read as it filled them, with its line where there is no uniform locker");
        string contract = ModSource(@"Core\GameContract.BizMan.cs");
        Check(contract.Contains("\"SetUpUniformsWindow\", \"skillNameDropdown\"") && contract.Contains("\"SetUpUniformsWindow\", \"uniformDropdown\"")
              && contract.Contains("\"SetUpUniformsWindow\", \"content\"") && contract.Contains("\"SetUpUniformsWindow\", \"noUniformsWarning\""),
            "bizman uniforms: the window's fields are named in the game contract");
        string page = ModSource(@"Ui\BizMan\BizManScreen.Page.cs");
        Check(page.Contains("GameContract.UniformRole, uniforms") && page.Contains("OptionsHash(preset)") && page.Contains("_roleOpen ? 64 : 0"),
            "bizman uniforms: the page redraws when the role or preset chosen, the presets' list or an open list changes");
        string back = MemberText(press, "private bool CloseLists()");
        Check(back.Contains("_roleOpen || _setOpen") && back.Contains("_roleOpen = _setOpen ="), "bizman uniforms: Back shuts an open role or preset list first");

        // The editor's name: the game names the preset when its field's editing ends, so each word typed ends it, not Enter alone.
        string editor = ModSource(@"Ui\BizMan\Ops\UniformsScreen.cs");
        string typed = MemberText(editor, "public void Typed(string text)");
        string name = MemberText(editor, "private void Name(string text, bool submit)");
        Check(typed.Contains("Name(text, false)") && name.Contains("UiWidgets.Type(f, text, submit)") && name.Contains("f.onEndEdit?.Invoke(f.text)")
              && editor.Contains("public void Submitted(string text) => Name(text, true);"),
            "bizman uniforms: a preset's name typed on the kit ends the game's field on every word, so a click away keeps it as Enter does");

        // The text.
        Dictionary<string, string> en = LocaleFile("en");
        Check(en.TryGetValue("reallife_ui_bizman_uniform_role", out string role) && role.Length > 0 && !role.Contains('!') && !role.Contains((char)0x2014),
            "bizman uniforms: the role label has its English words");
    }
}
