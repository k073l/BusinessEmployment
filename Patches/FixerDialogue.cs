using System.Collections;
using HarmonyLib;
using MelonLoader;
using ScheduleOne.Dialogue;
using ScheduleOne.Employees;
using ScheduleOne.Property;
using ScheduleOne.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BusinessEmployment.Patches;

[HarmonyPatch(typeof(DialogueController_Fixer))]
internal class FixerDialogue
{
    [HarmonyPatch("ModifyChoiceList")]
    [HarmonyPrefix]
    private static bool IncludeBusinesses(DialogueController_Fixer __instance, string dialogueLabel,
        ref List<DialogueChoiceData> existingChoices)
    {
        if (__instance == null) return true;
        if (dialogueLabel != "SELECT_LOCATION") return true;
        // we can limit the employee type to handler, but should we?
        // if (__instance.selectedEmployeeType != EEmployeeType.Handler) return true;
        foreach (var ownedProperty in Property.OwnedProperties)
        {
            if (ownedProperty.EmployeeCapacity >
                0) // we check capacity instead of what the game does, to try for compatibility with employee addition mods for RV and MotelRoom
            {
                existingChoices.Add(
                    new DialogueChoiceData
                    {
                        ChoiceText = ownedProperty.PropertyName,
                        ChoiceLabel = ownedProperty.PropertyCode
                    }
                );
            }
        }

        // hope we don't have to call the base method here...
        return false;
    }
}

[HarmonyPatch(typeof(DialogueCanvas), nameof(DialogueCanvas.RolloutDialogue))]
public static class RolloutDialogue_PrefixPatch
{
    static bool Prefix(
        DialogueCanvas __instance,
        string text,
        List<DialogueChoiceData> choices,
        ref IEnumerator __result)
    {
        __result = RolloutDialogueReplacement(__instance, text, choices);
        return false; // skip original, we replaced it
    }

    private static IEnumerator RolloutDialogueReplacement(
        DialogueCanvas __instance,
        string text,
        List<DialogueChoiceData> choices)
    {
        try
        {
            EnsureDialogueChoiceEntries(__instance, choices.Count);
        }
        catch (Exception e)
        {
            Melon<BusinessEmployment>.Logger.Error($"Caught exception while adding more dialogue choices: {e}");
        }


        var activeDialogueChoices = new List<int>();

        text = text.Replace("<color=red>", "<color=#FF6666>");
        text = text.Replace("<color=green>", "<color=#93FF58>");
        text = text.Replace("<color=blue>", "<color=#76C9FF>");

        __instance.dialogueText.maxVisibleCharacters = 0;
        __instance.dialogueText.text = text;
        __instance.canvas.enabled = true;
        __instance.Container.gameObject.SetActive(true);

        var rolloutTime = text.Length * 0.015f;

        if (__instance.SkipNextRollout)
        {
            __instance.SkipNextRollout = false;
            rolloutTime = 0f;
        }

        for (var i = 0f; i < rolloutTime; i += Time.deltaTime)
        {
            if (__instance.spaceDownThisFrame || __instance.leftClickThisFrame)
                break;

            int maxVisibleCharacters = (int)(i / 0.015f);
            __instance.dialogueText.maxVisibleCharacters = maxVisibleCharacters;

            yield return new WaitForEndOfFrame();
        }

        __instance.dialogueText.maxVisibleCharacters = text.Length;
        __instance.spaceDownThisFrame = false;
        __instance.leftClickThisFrame = false;
        __instance.hasChoiceBeenSelected = false;

        if (__instance.choiceSelectionResidualCoroutine != null)
        {
            __instance.StopCoroutine(__instance.choiceSelectionResidualCoroutine);
        }

        __instance.continuePopup.gameObject.SetActive(false);

        for (var j = 0; j < __instance.dialogueChoices.Count; j++)
        {
            var choiceUI = __instance.dialogueChoices[j];

            choiceUI.gameObject.SetActive(false);
            choiceUI.canvasGroup.alpha = 1f;

            if (choices.Count > j)
            {
                choiceUI.text.text = choices[j].ChoiceText;
                choiceUI.button.interactable = true;

                string reason;
                if (__instance.IsChoiceValid(j, out reason))
                {
                    choiceUI.notPossibleGameObject.SetActive(false);
                    choiceUI.button.interactable = true;

                    ColorBlock colors = choiceUI.button.colors;
                    colors.disabledColor = colors.pressedColor;
                    choiceUI.button.colors = colors;

                    choiceUI.text.GetComponent<RectTransform>().offsetMax = Vector2.zero;
                }
                else
                {
                    choiceUI.notPossibleText.text = reason.ToUpper();
                    choiceUI.notPossibleGameObject.SetActive(true);

                    ColorBlock colors = choiceUI.button.colors;
                    colors.disabledColor = colors.normalColor;
                    choiceUI.button.colors = colors;

                    choiceUI.button.interactable = false;
                    choiceUI.notPossibleText.ForceMeshUpdate();

                    choiceUI.text.GetComponent<RectTransform>().offsetMax =
                        new Vector2(-(choiceUI.notPossibleText.preferredWidth + 20f), 0f);
                }

                activeDialogueChoices.Add(j);
            }
        }

        if (activeDialogueChoices.Count == 0 ||
            (activeDialogueChoices.Count == 1 && choices[0].ChoiceText == ""))
        {
            __instance.continuePopup.gameObject.SetActive(true);

            yield return new WaitUntil(() =>
                __instance.spaceDownThisFrame || __instance.leftClickThisFrame);

            __instance.continuePopup.gameObject.SetActive(false);
            __instance.spaceDownThisFrame = false;
            __instance.leftClickThisFrame = false;

            __instance.currentHandler.ContinueSubmitted();
            yield break;
        }

        for (var k = 0; k < activeDialogueChoices.Count; k++)
        {
            __instance.dialogueChoices[activeDialogueChoices[k]]
                .gameObject.SetActive(true);
        }

        while (!__instance.hasChoiceBeenSelected)
        {
            string reason;

            if (Input.GetKey(KeyCode.Alpha1) && __instance.IsChoiceValid(0, out reason))
                __instance.ChoiceSelected(0);
            else if (Input.GetKey(KeyCode.Alpha2) && __instance.IsChoiceValid(1, out reason))
                __instance.ChoiceSelected(1);
            else if (Input.GetKey(KeyCode.Alpha3) && __instance.IsChoiceValid(2, out reason))
                __instance.ChoiceSelected(2);
            else if (Input.GetKey(KeyCode.Alpha4) && __instance.IsChoiceValid(3, out reason))
                __instance.ChoiceSelected(3);
            else if (Input.GetKey(KeyCode.Alpha5) && __instance.IsChoiceValid(4, out reason))
                __instance.ChoiceSelected(4);
            else if (Input.GetKey(KeyCode.Alpha6) && __instance.IsChoiceValid(5, out reason))
                __instance.ChoiceSelected(5);
            else if (Input.GetKey(KeyCode.Alpha7) && __instance.IsChoiceValid(6, out reason))
                __instance.ChoiceSelected(6);
            else if (Input.GetKey(KeyCode.Alpha8) && __instance.IsChoiceValid(7, out reason))
                __instance.ChoiceSelected(7);
            else if (Input.GetKey(KeyCode.Alpha9) && __instance.IsChoiceValid(8, out reason))
                __instance.ChoiceSelected(8);
            else if (Input.GetKey(KeyCode.Alpha0) && __instance.IsChoiceValid(9, out reason))
                __instance.ChoiceSelected(9);

            yield return new WaitForEndOfFrame();
        }
    }


    private static void EnsureDialogueChoiceEntries(
        DialogueCanvas canvas,
        int requiredCount)
    {
        if (canvas.dialogueChoices == null || canvas.dialogueChoices.Count == 0)
        {
            Melon<BusinessEmployment>.Logger.Error("DialogueChoices list is empty – cannot create template.");
            return;
        }

        var template = canvas.dialogueChoices[0];

        // cache relative paths
        var textPath = GetRelativePath(template.text?.transform, template.gameObject.transform);
        var buttonPath = GetRelativePath(template.button?.transform, template.gameObject.transform);
        var canvasGroupPath = GetRelativePath(template.canvasGroup?.transform, template.gameObject.transform);
        var notPossibleGOPath =
            GetRelativePath(template.notPossibleGameObject?.transform, template.gameObject.transform);
        var notPossibleTextPath = GetRelativePath(template.notPossibleText?.transform, template.gameObject.transform);

        while (canvas.dialogueChoices.Count < requiredCount)
        {
            int index = canvas.dialogueChoices.Count;

            var cloneGO = Object.Instantiate(
                template.gameObject,
                template.gameObject.transform.parent);

            cloneGO.name = $"Dialogue Choice {index}";
            cloneGO.SetActive(false);

            var entry = new DialogueChoiceEntry
            {
                gameObject = cloneGO,
                text = FindComponent<TextMeshProUGUI>(cloneGO, textPath),
                button = FindComponent<Button>(cloneGO, buttonPath),
                canvasGroup = FindComponent<CanvasGroup>(cloneGO, canvasGroupPath),
                notPossibleGameObject = FindTransform(cloneGO, notPossibleGOPath)?.gameObject,
                notPossibleText = FindComponent<TextMeshProUGUI>(cloneGO, notPossibleTextPath)
            };

            // validate
            if (entry.text == null ||
                entry.button == null ||
                entry.canvasGroup == null)
            {
                Melon<BusinessEmployment>.Logger.Error($"Failed to create DialogueChoiceEntry at index {index}");
                Object.Destroy(cloneGO);
                break;
            }

            var comp = FindComponent<TextMeshProUGUI>(cloneGO, "Background/Index");
            if (comp != null) comp.text = $"[{index + 1}]";
            else Melon<BusinessEmployment>.Logger.Error("Not found the index");

            // rebind the button
            entry.button.onClick.RemoveAllListeners();
            entry.button.onClick.AddListener(() => { canvas.ChoiceSelected(index); });

            canvas.dialogueChoices.Add(entry);
        }
    }

    private static string GetRelativePath(Transform target, Transform root)
    {
        if (target == null || root == null)
            return null;

        var stack = new Stack<string>();
        var current = target;

        while (current != null && current != root)
        {
            stack.Push(current.name);
            current = current.parent;
        }

        return current == root ? string.Join("/", stack) : null;
    }

    private static T FindComponent<T>(GameObject root, string path) where T : Component
    {
        if (root == null)
            return null;

        return string.IsNullOrEmpty(path) ? root.GetComponent<T>() : root.transform.Find(path)?.GetComponent<T>();
    }


    private static Transform FindTransform(GameObject root, string path)
    {
        if (root == null)
            return null;

        return string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path);
    }
}