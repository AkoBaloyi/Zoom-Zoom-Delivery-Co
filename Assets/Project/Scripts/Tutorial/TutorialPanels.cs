using UnityEngine;

// Shared check for the tutorial's hand-built panels.
//
// WHY THIS EXISTS
// Every tutorial panel does the same two things: switch its panelRoot on, then freeze time. If
// anything above that panel in the Hierarchy is switched off, SetActive(true) succeeds on the
// panel itself and changes nothing on screen, because a GameObject is only visible when it AND
// every parent above it is active. The game then freezes behind a panel nobody can see, which
// looks exactly like a crash and tells you nothing.
//
// This turns that silent lock-up into a named error, and lets the caller carry on with time
// running instead of freezing.
public static class TutorialPanels
{
    /// <summary>Call straight after panelRoot.SetActive(true) and before touching Time.timeScale.
    /// Returns true if the panel is genuinely on screen. If it is not, this names the exact
    /// GameObject that is blocking it and returns false, so the caller can refuse to freeze.</summary>
    public static bool VerifyVisible(GameObject panelRoot, Object context, string panelName)
    {
        if (panelRoot == null) return false;
        if (panelRoot.activeInHierarchy) return true;

        // Walk up to the topmost switched-off ancestor: that is the one actually hiding it, and
        // the one worth naming, since switching on anything below it changes nothing.
        Transform blocking = null;
        for (Transform t = panelRoot.transform; t != null; t = t.parent)
            if (!t.gameObject.activeSelf) blocking = t;

        Debug.LogError(
            $"[{panelName}] The panel was switched on but is still not visible, because " +
            $"\"{PathOf(blocking)}\" above it in the Hierarchy is switched off. A GameObject only " +
            "shows when it and every parent above it is active.\n" +
            "Time has been left running rather than freezing the game behind a panel you cannot " +
            "see. Move the panel out from under that object, or switch that object on.",
            context);

        return false;
    }

    private static string PathOf(Transform t)
    {
        if (t == null) return "(unknown)";

        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
