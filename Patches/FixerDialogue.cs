using System.Collections;
using HarmonyLib;
using MelonLoader;
#if MONO
using ScheduleOne.Dialogue;
using ScheduleOne.Property;
using ScheduleOne.UI;
#else
using Il2CppScheduleOne.Dialogue;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI;
#endif


namespace BusinessEmployment.Patches;

[HarmonyPatch(typeof(DialogueController_Fixer))]
internal class FixerDialogue
{
    [HarmonyPatch("ModifyChoiceList")]
    [HarmonyPrefix]
    private static bool IncludeBusinesses(DialogueController_Fixer __instance, string dialogueLabel,
#if MONO
        ref List<DialogueChoiceData> existingChoices)
#else
        ref Il2CppSystem.Collections.Generic.List<DialogueChoiceData> existingChoices)
#endif
    {
        if (__instance == null) return true;
        if (dialogueLabel != "SELECT_LOCATION") return true;

        var newChoices = new System.Collections.Generic.List<DialogueChoiceData>();

        foreach (var choice in existingChoices)
        {
            newChoices.Add(choice);
        }

        foreach (var ownedProperty in Property.OwnedProperties)
        {
            if (ownedProperty.EmployeeCapacity > 0)
            {
                newChoices.Add(new DialogueChoiceData
                {
                    ChoiceText = ownedProperty.PropertyName,
                    ChoiceLabel = ownedProperty.PropertyCode
                });
            }
        }

        // replace the il2cpp list because it wanted to be difficult
        existingChoices.Clear();
        foreach (var choice in newChoices)
        {
            existingChoices.Add(choice);
        }

        Melon<BusinessEmployment>.Logger.Msg("Sanity Check");
        foreach (var choice in existingChoices)
        {
            Melon<BusinessEmployment>.Logger.Msg($"{choice.ChoiceText}: {choice.ChoiceLabel}");
        }

        return false;
    }
}

/// <summary>
/// Paginated dialogue choices for DialogueCanvas.
/// Full credits to Khundiann (https://new.thunderstore.io/c/schedule-i/p/Khundian/) for the original implementation.
/// </summary>
[HarmonyPatch(typeof(DialogueCanvas))]
public static class DialoguePagingPatches
{
    private const int ChoicesPerPage = 7;

#if IL2CPP
    private static DialogueHandler _handler;
    private static DialogueNodeData _node;
    private static string _text;
    private static Il2CppSystem.Collections.Generic.List<DialogueChoiceData> _fullChoices;
#elif MONO
    private static DialogueHandler _handler;
    private static DialogueNodeData _node;
    private static string _text;
    private static System.Collections.Generic.List<DialogueChoiceData> _fullChoices;
#endif

    private static int _pageIndex;
    private static int _pageCount;

    private static bool PagingActive => _fullChoices != null && _pageCount > 1;

    private static int ClampInt(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

#if IL2CPP
    [HarmonyPatch("DisplayDialogueNode")]
    [HarmonyPrefix]
    private static void DisplayDialogueNodePrefix(DialogueHandler diag, DialogueNodeData node, string dialogueText,
        ref Il2CppSystem.Collections.Generic.List<DialogueChoiceData> choices)
    {
        try
        {
            if (choices == null || choices.Count <= 8)
            {
                ClearPagingIfNewNode(diag, node);
                return;
            }

            var isNewToken = _handler != diag || _node != node;
            if (isNewToken)
            {
                _handler = diag;
                _node = node;
                _text = dialogueText;
                _pageIndex = 0;
                _fullChoices = CopyList(choices);
                _pageCount = ComputePageCount(_fullChoices.Count);
            }
            else
            {
                _text = dialogueText;
                if (_fullChoices == null || _fullChoices.Count != choices.Count)
                {
                    _fullChoices = CopyList(choices);
                    _pageCount = ComputePageCount(_fullChoices.Count);
                    _pageIndex = ClampInt(_pageIndex, 0, Math.Max(0, _pageCount - 1));
                }
            }

            if (!PagingActive) return;
            choices = BuildPageChoices(_fullChoices, _pageIndex, _pageCount);
        }
        catch (Exception ex)
        {
            Melon<BusinessEmployment>.Logger.Error($"Dialogue paging DisplayDialogueNodePrefix failed: {ex}");
        }
    }
#elif MONO
    [HarmonyPatch("DisplayDialogueNode")]
    [HarmonyPrefix]
    private static void DisplayDialogueNodePrefix(DialogueHandler diag, DialogueNodeData node, string dialogueText, ref System.Collections.Generic.List<DialogueChoiceData> choices)
    {
        try
        {
            if (choices == null || choices.Count <= 8)
            {
                ClearPagingIfNewNode(diag, node);
                return;
            }

            bool isNewToken = _handler != diag || _node != node;
            if (isNewToken)
            {
                _handler = diag;
                _node = node;
                _text = dialogueText;
                _pageIndex = 0;
                _fullChoices = new System.Collections.Generic.List<DialogueChoiceData>(choices);
                _pageCount = ComputePageCount(_fullChoices.Count);
            }
            else
            {
                _text = dialogueText;
                if (_fullChoices == null || _fullChoices.Count != choices.Count)
                {
                    _fullChoices = new System.Collections.Generic.List<DialogueChoiceData>(choices);
                    _pageCount = ComputePageCount(_fullChoices.Count);
                    _pageIndex = ClampInt(_pageIndex, 0, Math.Max(0, _pageCount - 1));
                }
            }

            if (!PagingActive) return;
            choices = BuildPageChoices(_fullChoices, _pageIndex, _pageCount);
        }
        catch (Exception ex)
        {
            Melon<BusinessEmployment>.Logger.Error($"Dialogue paging DisplayDialogueNodePrefix failed: {ex}");
        }
    }
#endif

    [HarmonyPatch("DisplayDialogueNode")]
    [HarmonyPostfix]
    private static void DisplayDialogueNodePostfix(DialogueHandler diag)
    {
        try
        {
            if (!PagingActive) return;
            if (_handler != diag) return;
            diag.CurrentChoices = _fullChoices;
        }
        catch
        {
        }
    }

    [HarmonyPatch("ChoiceSelected")]
    [HarmonyPrefix]
    private static bool ChoiceSelectedPrefix(DialogueCanvas __instance, ref int choiceIndex)
    {
        try
        {
            if (!PagingActive) return true;
            if (__instance == null) return true;
            if (_fullChoices == null || _handler == null || _node == null || _text == null) return true;

            var total = _fullChoices.Count;
            var pageStart = _pageIndex * ChoicesPerPage;
            var remaining = Math.Max(0, total - pageStart);
            var realOnPage = Math.Min(ChoicesPerPage, remaining);

            if (choiceIndex == realOnPage)
            {
                _pageIndex = (_pageIndex + 1) % _pageCount;
                __instance.SkipNextRollout = true;
                __instance.DisplayDialogueNode(_handler, _node, _text, _fullChoices);
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= realOnPage) return true;
            choiceIndex = pageStart + choiceIndex;
            return true;
        }
        catch (Exception ex)
        {
            Melon<BusinessEmployment>.Logger.Error($"Dialogue paging ChoiceSelectedPrefix failed: {ex}");
            return true;
        }
    }

    [HarmonyPatch("EndDialogue")]
    [HarmonyPostfix]
    private static void EndDialoguePostfix()
    {
        ClearPaging();
    }

    [HarmonyPatch("IsChoiceValid")]
    [HarmonyPrefix]
    private static bool IsChoiceValidPrefix(int choiceIndex, ref string reason, ref bool __result)
    {
        try
        {
            if (!PagingActive) return true;
            if (_handler == null || _fullChoices == null) return true;

            // check if this is a page-relative index (0 to realOnPage)
            var total = _fullChoices.Count;
            var pageStart = _pageIndex * ChoicesPerPage;
            var remaining = Math.Max(0, total - pageStart);
            var realOnPage = Math.Min(ChoicesPerPage, remaining);

            // if choiceIndex is within page bounds, it's a page-relative index
            if (choiceIndex <= realOnPage)
            {
                if (IsMoreIndex(choiceIndex))
                {
                    reason = string.Empty;
                    __result = true;
                    return false;
                }

                if (choiceIndex < 0 || choiceIndex >= realOnPage)
                {
                    reason = string.Empty;
                    __result = false;
                    return false;
                }

                // map to real
                choiceIndex = pageStart + choiceIndex;
            }

            // validate using the real index against CurrentChoices
            var current = _handler.CurrentChoices;
            if (current == null || choiceIndex < 0 || choiceIndex >= current.Count)
            {
                reason = string.Empty;
                __result = false;
                return false;
            }

            var data = current[choiceIndex];
            var label = data?.ChoiceLabel ?? string.Empty;

            if (string.IsNullOrWhiteSpace(label))
            {
                reason = string.Empty;
                __result = false;
                return false;
            }

            string invalidReason;
            var ok = _handler.CheckChoice(label, out invalidReason);
            reason = invalidReason;
            __result = ok;
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsMoreIndex(int choiceIndex)
    {
        if (!PagingActive) return false;
        if (_fullChoices == null) return false;

        var total = _fullChoices.Count;
        var pageStart = _pageIndex * ChoicesPerPage;
        var remaining = Math.Max(0, total - pageStart);
        var realOnPage = Math.Min(ChoicesPerPage, remaining);
        return choiceIndex == realOnPage;
    }

    private static void ClearPagingIfNewNode(DialogueHandler diag, DialogueNodeData node)
    {
        if (_handler != diag || _node != node) ClearPaging();
    }

    private static void ClearPaging()
    {
        _handler = null;
        _node = null;
        _text = null;
        _fullChoices = null;
        _pageIndex = 0;
        _pageCount = 0;
    }

    private static int ComputePageCount(int totalChoices)
    {
        if (totalChoices <= 8) return 0;
        return (totalChoices + ChoicesPerPage - 1) / ChoicesPerPage;
    }

#if IL2CPP
    private static Il2CppSystem.Collections.Generic.List<DialogueChoiceData> CopyList(
        Il2CppSystem.Collections.Generic.List<DialogueChoiceData> src)
    {
        var dst = new Il2CppSystem.Collections.Generic.List<DialogueChoiceData>();
        if (src == null) return dst;
        for (var i = 0; i < src.Count; i++)
        {
            var item = src[i];
            if (item != null) dst.Add(item);
        }

        return dst;
    }

    private static Il2CppSystem.Collections.Generic.List<DialogueChoiceData> BuildPageChoices(
        Il2CppSystem.Collections.Generic.List<DialogueChoiceData> full, int pageIndex, int pageCount)
    {
        var page = new Il2CppSystem.Collections.Generic.List<DialogueChoiceData>();
        var total = full.Count;
        var start = pageIndex * ChoicesPerPage;
        var take = Math.Min(ChoicesPerPage, Math.Max(0, total - start));

        for (var i = 0; i < take; i++)
        {
            var c = full[start + i];
            if (c != null) page.Add(c);
        }

        var borrowedLabel = string.Empty;
        try
        {
            var nextPageIndex = (pageIndex + 1) % pageCount;
            var nextStart = nextPageIndex * ChoicesPerPage;
            if (nextStart >= 0 && nextStart < full.Count)
            {
                var next = full[nextStart];
                if (next != null && !string.IsNullOrWhiteSpace(next.ChoiceLabel))
                    borrowedLabel = next.ChoiceLabel;
            }
        }
        catch
        {
        }

        page.Add(new DialogueChoiceData
        {
            ChoiceLabel = borrowedLabel,
            ChoiceText = $"More ({pageIndex + 1}/{pageCount})",
        });

        return page;
    }
#elif MONO
    private static System.Collections.Generic.List<DialogueChoiceData> BuildPageChoices(System.Collections.Generic.List<DialogueChoiceData> full, int pageIndex, int pageCount)
    {
        var page = new System.Collections.Generic.List<DialogueChoiceData>();
        int total = full.Count;
        int start = pageIndex * ChoicesPerPage;
        int take = Math.Min(ChoicesPerPage, Math.Max(0, total - start));
        
        for (int i = 0; i < take; i++)
        {
            var c = full[start + i];
            if (c != null) page.Add(c);
        }

        string borrowedLabel = string.Empty;
        try
        {
            int nextPageIndex = (pageIndex + 1) % pageCount;
            int nextStart = nextPageIndex * ChoicesPerPage;
            if (nextStart >= 0 && nextStart < full.Count)
            {
                var next = full[nextStart];
                if (next != null && !string.IsNullOrWhiteSpace(next.ChoiceLabel))
                    borrowedLabel = next.ChoiceLabel;
            }
        }
        catch { }

        page.Add(new DialogueChoiceData
        {
            ChoiceLabel = borrowedLabel,
            ChoiceText = $"More ({pageIndex + 1}/{pageCount})",
        });

        return page;
    }
#endif
}