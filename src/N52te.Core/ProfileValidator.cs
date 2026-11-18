namespace N52te;

public enum IssueSeverity { Warning, Error }

public sealed record ProfileIssue(IssueSeverity Severity, string Message, Keymap? Keymap = null, int? Input = null)
{
    public override string ToString()
    {
        var where = Keymap is null ? "" : Input is null ? $"{Keymap}: " : $"{Keymap}, {KeypadLayout.Input(Input.Value).Name}: ";
        return $"{Severity}: {where}{Message}";
    }
}

/// <summary>Checks a profile before it is saved to the keypad or exported.</summary>
public static class ProfileValidator
{
    public static List<ProfileIssue> Validate(Profile profile)
    {
        var issues = new List<ProfileIssue>();

        for (int k = 0; k < KeypadLayout.KeymapCount; k++)
            for (int n = 1; n <= KeypadLayout.InputCount; n++)
            {
                var keymap = (Keymap)k;
                var assignment = profile.Keymaps[k].Inputs[n - 1];
                try { ProfileCompiler.Encode(n, assignment, CompanionChords.All[0]); }
                catch (ProfileException e) { issues.Add(new(IssueSeverity.Error, e.Message, keymap, n)); }

                switch (assignment.Action)
                {
                    case LegacyPcAction:
                        issues.Add(new(IssueSeverity.Warning, "Belkin PC action from an old profile; it does nothing without Belkin's software. Replace it with a Companion action.", keymap, n));
                        break;
                    case RawAction:
                        issues.Add(new(IssueSeverity.Warning, "Unrecognised record kept as it was imported.", keymap, n));
                        break;
                    case CompanionAction { Kind: CompanionKind.LaunchProgram or CompanionKind.OpenUrl or CompanionKind.TypeText } c
                        when string.IsNullOrWhiteSpace(c.Argument):
                        issues.Add(new(IssueSeverity.Error, "Companion action needs a program, address or text.", keymap, n));
                        break;
                }

                if (assignment.Action is KeymapSwitchAction)
                {
                    for (int other = 0; other < KeypadLayout.KeymapCount; other++)
                        if (profile.Keymaps[other].Inputs[n - 1].Action != assignment.Action)
                        {
                            issues.Add(new(IssueSeverity.Warning, "Keymap switch is not on this button in every keymap, so you may not be able to switch back.", keymap, n));
                            break;
                        }
                }
            }

        try { CompanionChords.Allocate(profile); }
        catch (ProfileException e) { issues.Add(new(IssueSeverity.Error, e.Message)); }

        var switches = profile.Keymaps.SelectMany(k => k.Inputs).Select(i => i.Action).OfType<KeymapSwitchAction>().Select(s => s.Switch).ToHashSet();
        bool leavesBlue = switches.Overlaps(new[] { KeymapSwitch.GreenToggle, KeymapSwitch.RedToggle });
        bool returnsToBlue = switches.Overlaps(new[] { KeymapSwitch.BlueToggle, KeymapSwitch.Cycle });
        if (leavesBlue && !returnsToBlue)
            issues.Add(new(IssueSeverity.Warning, "A toggle switches away from Blue but nothing toggles back (add a Blue toggle or Cycle)."));

        if (profile.PollingIntervalMs is { } ms && ms is not (1 or 2 or 8))
            issues.Add(new(IssueSeverity.Error, "Polling interval must be 1, 2 or 8 ms."));

        bool usesKeymaps = switches.Count > 0;
        if (!usesKeymaps && profile.StartKeymap != Keymap.Blue && profile.AppRules.Count == 0)
            issues.Add(new(IssueSeverity.Warning, $"Start keymap is {profile.StartKeymap} but no button switches keymaps."));

        return issues;
    }
}
