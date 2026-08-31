#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System;

public class StoryFlowGraphView : GraphView
{
    /* Asks the host window to rebuild its side menu. */
    public Action RequestMenuRefresh { get; set; }
    public Action RequestReload { get; set; }

    private readonly Dictionary<string, StoryCard> _cards = new();   // StoryID(name) → StoryCard
    public  List<GraphElement> DisplayOrder { get; } = new();        // Ordering used by the side menu.

    private readonly string _storyPath;
    private readonly string _branchPath;
    private readonly string _scriptFolder;
    private readonly string _subStoryPath = "Stories/SubStoryDatas";

    public StoryFlowGraphView(string storyPath, string branchPath, string scriptFolder)
    {
        _storyPath    = storyPath;
        _branchPath   = branchPath;
        _scriptFolder = scriptFolder;

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        var grid = new GridBackground(); Insert(0, grid); grid.StretchToParentSize();

        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        this.AddManipulator(new ContextualMenuManipulator(BuildCtxMenu));

        // Node moves are persisted immediately; there is no explicit save step.
        graphViewChanged = OnGraphViewChanged;
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        
        if (change.movedElements != null && change.movedElements.Count > 0)
        {
            foreach (var element in change.movedElements)
            {
                
                if (element is StoryCard storyCard)
                {
                    storyCard.SaveToData();
                }
                else if (element is BranchCard branchCard)
                {
                    branchCard.SaveToData();
                }
                else if (element is SubStoryCard subStoryCard)
                {
                    subStoryCard.SaveToData();
                }
            }
        }

        return change;
    }

    /* Context menu on empty canvas. */
    private void BuildCtxMenu(ContextualMenuPopulateEvent e)
    {
        Vector2 mp = e.mousePosition;
        e.menu.AppendAction("Add Story Node",  _ => AddStoryNode(mp));
        e.menu.AppendAction("Add Branch Node", _ => AddBranchNode(mp));
        e.menu.AppendAction("Add SubStory Node", _ => AddSubStoryNode(mp));
        e.menu.AppendSeparator();
        e.menu.AppendAction("Save Graph",      _ => SaveGraph());
    }

    /* Node creation. */
    private void AddStoryNode(Vector2 worldPos)
    {
        StoryDataNamePopup.ShowPopup(name =>
        {
            if (string.IsNullOrEmpty(name)) return;
            Vector2 local = contentViewContainer.WorldToLocal(worldPos);

            var data = ScriptableObject.CreateInstance<StoryData>();
            data.StoryName_KO = name;
            data.storyID = name;
            data.NodePosition = local;
            AssetDatabase.CreateAsset(data, $"Assets/Resources/{_storyPath}/{name}.asset");
            AssetDatabase.SaveAssets();

            var card = new StoryCard(data);
            AddElement(card);
            card.SetPosition(new Rect(local, new Vector2(200, 150)));

            InjectMenuAndPing(card);

            RequestMenuRefresh?.Invoke();
        });
    }

    private void AddBranchNode(Vector2 worldPos)
    {
        Vector2 local = contentViewContainer.WorldToLocal(worldPos);

        // The asset name is the card id, so the id is chosen up front.
        TextPromptWindow.Show(
            title: "Add Branch Node",
            label: "Branch Card ID (A-z, 0-9, _ only)",
            initial: "Branch_",
            allowEmpty: false,
            onOk: branchID =>
            {
                // Validate the id format.
                if (!Regex.IsMatch(branchID, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    EditorUtility.DisplayDialog("Invalid ID", "Use A-z, 0-9, _. Must not start with a digit.", "OK");
                    return;
                }

                // Reject an id that already has a BranchData asset.
                string assetPath = $"Assets/Resources/{_branchPath}/{branchID}.asset";
                if (AssetDatabase.LoadAssetAtPath<BranchData>(assetPath) != null)
                {
                    EditorUtility.DisplayDialog("Duplicate", $"Branch Card ID '{branchID}' already exists.", "OK");
                    return;
                }

                var data = ScriptableObject.CreateInstance<BranchData>();
                data.name         = branchID;
                data.NodePosition = local;
                data.groups.Add(new BranchData.Group());
                AssetDatabase.CreateAsset(data, assetPath);
                AssetDatabase.SaveAssets();

                var card = new BranchCard(data);
                AddElement(card);
                card.SetPosition(new Rect(local, new Vector2(320, 190)));

                RequestMenuRefresh?.Invoke();
                Debug.Log($"[StoryFlowEditor] Branch created: {branchID}");
            });
    }

    private void AddSubStoryNode(Vector2 worldPos)
    {
        TextPromptWindow.Show(
            title: "Add SubStory Node",
            label: "SubStory Card ID (A-z, 0-9, _ only)",
            initial: "SubStory_",
            allowEmpty: false,
            onOk: cardID =>
            {
                // Validate the id format.
                if (!Regex.IsMatch(cardID, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    EditorUtility.DisplayDialog("Invalid ID", "Use A-z, 0-9, _. Must not start with a digit.", "OK");
                    return;
                }

                // Reject a duplicate id.
                string assetPath = $"Assets/Resources/{_subStoryPath}/{cardID}.asset";
                if (AssetDatabase.LoadAssetAtPath<SubStoryData>(assetPath) != null)
                {
                    EditorUtility.DisplayDialog("Duplicate", $"SubStory ID '{cardID}' already exists.", "OK");
                    return;
                }

                Vector2 local = contentViewContainer.WorldToLocal(worldPos);

                var data = ScriptableObject.CreateInstance<SubStoryData>();
                data.name = cardID;
                data.NodePosition = local;

                Directory.CreateDirectory($"Assets/Resources/{_subStoryPath}");
                AssetDatabase.CreateAsset(data, assetPath);
                AssetDatabase.SaveAssets();

                var card = new SubStoryCard(data);
                AddElement(card);
                card.SetPosition(new Rect(local, new Vector2(400, 200)));

                AttachSubStoryRename(card);

                RequestMenuRefresh?.Invoke();
                Debug.Log($"[StoryFlowEditor] SubStory created: {cardID}");
            });
    }

    /* Cloning a story: shared content, independent connections. */
    private void CreateClone(StoryCard originalCard)
    {
        // Suffix appended to the original id, for example "_BadEnding".
        TextPromptWindow.Show(
            title: "Create Clone",
            label: $"Clone Suffix for '{originalCard.Data.StoryID}' (e.g., '_Clone1', '_BadEnding')",
            initial: "_Clone1",
            allowEmpty: false,
            onOk: cloneSuffix =>
            {
                if (string.IsNullOrEmpty(cloneSuffix)) return;

                if (!cloneSuffix.StartsWith("_"))
                    cloneSuffix = "_" + cloneSuffix;

                Vector2 offset = new Vector2(50, 50);
                Vector2 clonePos = originalCard.GetPosition().position + offset;

                var cloneData = ScriptableObject.CreateInstance<StoryData>();

                cloneData.IsClone = true;
                cloneData.OriginalStoryID = originalCard.Data.StoryIDToPlay;
                cloneData.CloneSuffix = cloneSuffix;

                // Copied from the original and read-only on the clone.
                cloneData.StoryName_KO = originalCard.Data.StoryName_KO;
                cloneData.StoryThumbnail = originalCard.Data.StoryThumbnail;
                cloneData.StoryColor = originalCard.Data.StoryColor;
                cloneData.StoryBuildVersion = originalCard.Data.StoryBuildVersion;

                // Owned by the clone, so an alternate ending can rejoin the flow elsewhere.
                cloneData.NextCardID = "";
                cloneData.Memo = "";
                cloneData.UseMemo = false;

                // Editor-only.
                cloneData.NodePosition = clonePos;

                string assetName = originalCard.Data.StoryID + cloneSuffix;
                string assetPath = $"Assets/Resources/{_storyPath}/{assetName}.asset";
                cloneData.storyID = assetName;

                if (AssetDatabase.LoadAssetAtPath<StoryData>(assetPath) != null)
                {
                    EditorUtility.DisplayDialog("Clone Error",
                        $"Clone '{assetName}' already exists!", "OK");
                    return;
                }

                AssetDatabase.CreateAsset(cloneData, assetPath);
                AssetDatabase.SaveAssets();

                var cloneCard = new StoryCard(cloneData);
                AddElement(cloneCard);
                cloneCard.SetPosition(new Rect(clonePos, StoryCardUI.CardSize));
                _cards[cloneData.StoryID] = cloneCard;

                InjectMenuAndPing(cloneCard);

                RequestMenuRefresh?.Invoke();
                Debug.Log($"[Clone] Created clone '{cloneData.StoryID}' (suffix: {cloneSuffix}) of '{originalCard.Data.StoryID}' at {assetPath}");
            });
    }

    /* Story-only context menu entries. */
    void InjectMenuAndPing(StoryCard card)
    {
        card.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            // A clone shares the original's script, so it cannot rename or regenerate it.
            if (!card.Data.IsClone)
            {
                string path = Path.Combine(_scriptFolder, $"{card.Data.StoryID}.cs");
                if (File.Exists(path))
                    evt.menu.AppendAction("Edit Script", _ => OpenScript(path));
                else
                    evt.menu.AppendAction("Make Script", _ => CreateScript(path, card.Data.StoryID));

                evt.menu.AppendAction("Rename Story...", _ => PromptRenameStory(card));
                evt.menu.AppendSeparator();

                // Only an original can be cloned; clones do not nest.
                evt.menu.AppendAction("Create Clone", _ => CreateClone(card));
            }
            else
            {
                
                evt.menu.AppendAction("Go to Original", _ =>
                {
                    var originalPath = $"Assets/Resources/{_storyPath}/{card.Data.OriginalStoryID}.asset";
                    var original = AssetDatabase.LoadAssetAtPath<StoryData>(originalPath);
                    if (original != null)
                    {
                        EditorGUIUtility.PingObject(original);
                        Debug.Log($"Original Story: {original.StoryName_KO} ({original.StoryID})");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Original Not Found",
                            $"Original Story '{card.Data.OriginalStoryID}' not found!", "OK");
                    }
                });
            }

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Total Delete (Deletes All Resources)", _ => TotalDeleteStory(card));
        }));

    }

    /*──────── Total Delete Story ─────*/
    private void TotalDeleteStory(StoryCard card)
    {
        string storyID = card.Data.StoryID;

        // 1. Find what actually exists.
        string dataPath = AssetDatabase.GetAssetPath(card.Data);
        string scriptPath = Path.Combine(_scriptFolder, $"{storyID}.cs");
        string imaginationFolder = $"Assets/Resources/Imaginations/{storyID}";

        bool hasData = !string.IsNullOrEmpty(dataPath);
        bool hasScript = File.Exists(scriptPath);
        bool hasImagination = AssetDatabase.IsValidFolder(imaginationFolder);

        // 2. Build the confirmation message.
        string message = $"Delete every resource belonging to story '{card.Data.StoryName_KO}' ({storyID})?\n\n";
        message += "Will be deleted:\n";
        message += $"• ScriptableObject: {(hasData ? "found" : "missing")}\n";
        message += $"• Script (.cs): {(hasScript ? "found" : "missing")}\n";
        message += $"- Imaginations folder: {(hasImagination ? "found" : "missing")}\n\n";
        message += "This cannot be undone.";

        // 3. Confirm.
        if (!EditorUtility.DisplayDialog("Total Delete", message, "Delete", "Cancel"))
        {
            Debug.Log($"[TotalDelete] cancelled: {storyID}");
            return;
        }

        // 4. Delete.
        AssetDatabase.StartAssetEditing();
        try
        {
            List<string> deletedItems = new List<string>();

            if (hasData)
            {
                AssetDatabase.DeleteAsset(dataPath);
                deletedItems.Add("ScriptableObject");
                Debug.Log($"[TotalDelete] Deleted Data: {dataPath}");
            }

            if (hasScript)
            {
                string metaPath = scriptPath + ".meta";
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                    deletedItems.Add("Script (.cs)");
                    Debug.Log($"[TotalDelete] Deleted Script: {scriptPath}");
                }
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                    Debug.Log($"[TotalDelete] Deleted Meta: {metaPath}");
                }
            }

            if (hasImagination)
            {
                AssetDatabase.DeleteAsset(imaginationFolder);
                deletedItems.Add("Imaginations folder");
                Debug.Log($"[TotalDelete] Deleted Imaginations: {imaginationFolder}");
            }

            // Drop the node from the graph.
            if (_cards.Remove(storyID))
            {
                RemoveElement(card);
                deletedItems.Add("Graph Node");
                Debug.Log($"[TotalDelete] Removed from graph: {storyID}");
            }

            Debug.Log($"[TotalDelete] done: {storyID} - deleted: {string.Join(", ", deletedItems)}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SaveGraph(); // Rewrite NextCardID on whatever the deletion disconnected.
        RequestMenuRefresh?.Invoke();

        EditorUtility.DisplayDialog("Total Delete complete",
            $"Deleted every resource for '{card.Data.StoryName_KO}' ({storyID}).", "OK");
    }

    private void RenameImaginationFolder(string oldId, string newId)
    {
        const string root = "Assets/Resources/Imaginations";
        // Nothing to move if the root was never created.
        if (!AssetDatabase.IsValidFolder(root))
        {
            Directory.CreateDirectory(root);
            Debug.Log($"[Imagination] Root created: {root}. No folder to rename for '{oldId}'.");
            return;
        }

        string src = $"{root}/{oldId}";
        if (!AssetDatabase.IsValidFolder(src))
        {
            Debug.Log($"[Imagination] No folder to rename: {src}");
            return;
        }

        string dst = $"{root}/{newId}";
        if (AssetDatabase.IsValidFolder(dst))
        {
            // Destination taken: fall back to a free name instead of overwriting.
            string fallback = $"{root}/{newId}_migrated_{DateTime.Now:yyyyMMdd_HHmmss}";
            string errAlt = AssetDatabase.MoveAsset(src.Replace("\\","/"), fallback.Replace("\\","/"));
            if (!string.IsNullOrEmpty(errAlt))
            {
                Debug.LogError($"[Imagination] Move failed (dst exists). src='{src}' → fallback='{fallback}' :: {errAlt}");
                return;
            }
            Debug.LogWarning($"[Imagination] Destination existed. Moved to fallback: {fallback}");
            return;
        }

        string err = AssetDatabase.MoveAsset(src.Replace("\\","/"), dst.Replace("\\","/"));
        if (!string.IsNullOrEmpty(err))
        {
            Debug.LogError($"[Imagination] Move failed: '{src}' → '{dst}' :: {err}");
        }
        else
        {
            Debug.Log($"[Imagination] Folder renamed: '{src}' → '{dst}'");
        }
    }

    private void PromptRenameStory(StoryCard card)
    {
        TextPromptWindow.Show(
            title  : "Rename Story",
            label  : "New Story ID (A-z, 0-9, _ only)",
            initial: card.Data.name,
            allowEmpty: false,
            onOk: newId =>
            {
                if (!Regex.IsMatch(newId, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    EditorUtility.DisplayDialog("Invalid ID", "Use A-z, 0-9, _. Must not start with a digit.", "OK");
                    return;
                }
                if (_cards.ContainsKey(newId))
                {
                    EditorUtility.DisplayDialog("Duplicate", $"StoryID '{newId}' already exists.", "OK");
                    return;
                }

                string oldId = card.Data.name;
                string dataPath = AssetDatabase.GetAssetPath(card.Data);
                string oldScriptPath = Path.Combine(_scriptFolder, $"{oldId}.cs");
                string newScriptPath = Path.Combine(_scriptFolder, $"{newId}.cs");

                AssetDatabase.StartAssetEditing();
                try
                {
                    // 1. Rename the asset, which is what defines StoryID. Nothing else
                    // happens if this fails: carrying on would leave the asset, the field
                    // and the script under three different names.
                    string err = AssetDatabase.RenameAsset(dataPath, newId);
                    if (!string.IsNullOrEmpty(err))
                    {
                        Debug.LogError($"[RenameStory] '{oldId}' → '{newId}' failed, nothing was changed: {err}");
                        EditorUtility.DisplayDialog("Rename failed", err, "OK");
                        return;
                    }

                    card.Data.storyID = newId;
                    EditorUtility.SetDirty(card.Data);

                    // 2. Keep the generated script's class and file name in step.
                    if (File.Exists(oldScriptPath))
                    {
                        string code = File.ReadAllText(oldScriptPath);
                        code = Regex.Replace(code, $@"\bclass\s+{Regex.Escape(oldId)}\b", $"class {newId}");
                        File.WriteAllText(oldScriptPath, code);

                        string moveErr = AssetDatabase.MoveAsset(
                            oldScriptPath.Replace("\\", "/"),
                            newScriptPath.Replace("\\", "/"));
                        if (!string.IsNullOrEmpty(moveErr))
                        {
                            // The class inside was already renamed, so the file name is now
                            // the odd one out and has to be fixed by hand.
                            Debug.LogError(
                                $"[RenameStory] Class renamed to '{newId}' but the file is still " +
                                $"'{Path.GetFileName(oldScriptPath)}': {moveErr}");
                        }
                    }

                    // 3. Update the in-memory lookup.
                    if (_cards.Remove(oldId))
                        _cards[newId] = card;

                    // 4. Update the node title.
                    card.title = $"{card.Data.StoryName_KO} ({card.Data.StoryID})";
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                // Move the story's Imaginations folder along with it, or the rename
            // silently orphans its art.
                RenameImaginationFolder(oldId, newId);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Reload so every reference is rebuilt from disk.
                RequestReload?.Invoke();

                Debug.Log($"[StoryFlowEditor] Story renamed: {oldId} → {newId}, reloaded");
            });
    }

    /*──────── Total Delete Branch ─────*/
    private void TotalDeleteBranch(BranchCard bc)
    {
        string branchName = bc.Data.name;

        // 1. Find what actually exists.
        string dataPath = AssetDatabase.GetAssetPath(bc.Data);
        bool hasData = !string.IsNullOrEmpty(dataPath);

        // 2. Build the confirmation message.
        string message = $"Delete branch '{branchName}'?\n\n";
        message += "Will be deleted:\n";
        message += $"• BranchData: {(hasData ? "found" : "missing")}\n\n";
        message += "This cannot be undone.";

        // 3. Confirm.
        if (!EditorUtility.DisplayDialog("Total Delete Branch", message, "Delete", "Cancel"))
        {
            Debug.Log($"[TotalDeleteBranch] cancelled: {branchName}");
            return;
        }

        // 4. Delete.
        AssetDatabase.StartAssetEditing();
        try
        {
            
            if (hasData)
            {
                AssetDatabase.DeleteAsset(dataPath);
                Debug.Log($"[TotalDeleteBranch] Deleted Data: {dataPath}");
            }

            // Drop the node from the graph.
            RemoveElement(bc);
            Debug.Log($"[TotalDeleteBranch] Removed from graph: {branchName}");

            Debug.Log($"[TotalDeleteBranch] done: {branchName}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SaveGraph(); // Rewrite NextCardID on whatever the deletion disconnected.
        RequestMenuRefresh?.Invoke();

        EditorUtility.DisplayDialog("Total Delete complete",
            $"Deleted branch '{branchName}'.", "OK");
    }

    /* Renaming a sub-story. */
    private void AttachSubStoryRename(SubStoryCard sc)
    {
        sc.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            evt.menu.AppendAction("Rename SubStory...", _ =>
            {
                TextPromptWindow.Show(
                    title: "Rename SubStory",
                    label: "New SubStory Card ID (A-z, 0-9, _ only)",
                    initial: sc.Data.CardID,
                    allowEmpty: false,
                    onOk: newID =>
                    {
                        // Validate the id format.
                        if (!Regex.IsMatch(newID, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                        {
                            EditorUtility.DisplayDialog("Invalid ID", "Use A-z, 0-9, _. Must not start with a digit.", "OK");
                            return;
                        }

                        // Reject a duplicate id.
                        string newAssetPath = $"Assets/Resources/{_subStoryPath}/{newID}.asset";
                        if (AssetDatabase.LoadAssetAtPath<SubStoryData>(newAssetPath) != null)
                        {
                            EditorUtility.DisplayDialog("Duplicate", $"SubStory ID '{newID}' already exists.", "OK");
                            return;
                        }

                        string oldID = sc.Data.CardID;
                        string dataPath = AssetDatabase.GetAssetPath(sc.Data);

                        AssetDatabase.StartAssetEditing();
                        try
                        {
                            // 1. Rename the asset.
                            string err = AssetDatabase.RenameAsset(dataPath, newID);
                            if (!string.IsNullOrEmpty(err))
                            {
                                Debug.LogError($"[SubStoryRename] Failed to rename asset: {err}");
                                return;
                            }

                            EditorUtility.SetDirty(sc.Data);

                            // 2. CardID reads from the asset name, so the title follows.
                            sc.title = $"<SubStory> {sc.Data.CardID}";

                            AssetDatabase.SaveAssets();

                            Debug.Log($"[SubStoryCard] Renamed: {oldID} → {newID}");
                        }
                        finally
                        {
                            AssetDatabase.StopAssetEditing();
                        }

                        AssetDatabase.Refresh();

                        // Reload so every reference is rebuilt from disk.
                        RequestReload?.Invoke();

                        Debug.Log($"[SubStoryCard] Renamed done: {oldID} → {newID}, reloaded");
                    });
            });

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Total Delete ( Deletes SubStory)", _ => TotalDeleteSubStory(sc));
        }));
    }

    /*──────── Total Delete SubStory ─────*/
    private void TotalDeleteSubStory(SubStoryCard sc)
    {
        string subStoryName = sc.Data.name;
        string cardID = sc.Data.CardID;

        // 1. Find what actually exists.
        string dataPath = AssetDatabase.GetAssetPath(sc.Data);
        bool hasData = !string.IsNullOrEmpty(dataPath);

        // 2. Build the confirmation message.
        string message = $"Delete sub-story '{subStoryName}' (card id {cardID})?\n\n";
        message += "Will be deleted:\n";
        message += $"• SubStoryData: {(hasData ? "found" : "missing")}\n\n";
        message += "This cannot be undone.";

        // 3. Confirm.
        if (!EditorUtility.DisplayDialog("Total Delete SubStory", message, "Delete", "Cancel"))
        {
            Debug.Log($"[TotalDeleteSubStory] cancelled: {subStoryName}");
            return;
        }

        // 4. Delete.
        AssetDatabase.StartAssetEditing();
        try
        {
            
            if (hasData)
            {
                AssetDatabase.DeleteAsset(dataPath);
                Debug.Log($"[TotalDeleteSubStory] Deleted Data: {dataPath}");
            }

            // Drop the node from the graph.
            RemoveElement(sc);
            Debug.Log($"[TotalDeleteSubStory] Removed from graph: {subStoryName}");

            Debug.Log($"[TotalDeleteSubStory] done: {subStoryName}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SaveGraph(); // Rewrite NextCardID on whatever the deletion disconnected.
        RequestMenuRefresh?.Invoke();

        EditorUtility.DisplayDialog("Total Delete complete",
            $"Deleted sub-story '{subStoryName}'.", "OK");
    }

    /* Opening or generating a story's script. */
    private static void OpenScript(string path)
    {
        var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        if (mono) AssetDatabase.OpenAsset(mono);
    }

    private static void CreateScript(string path, string className)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) return;

        string tpl = $@"using UnityEngine; using System.Collections.Generic;
using static CharacterParameters; 
using static CNames;
public class {className} : Story {{ public override List<Element> UpdateElements => new List<Element>{{}}; }}";
        File.WriteAllText(path, tpl);
        AssetDatabase.Refresh();
    }

    /* Persisting the graph back into the ScriptableObjects. */
    public void SaveGraph()
    {
        int savedCount = 0;
        foreach (var node in graphElements)
        {
            switch (node)
            {
                case StoryCard sc:
                    savedCount++;
                    
                    var next = sc.OutputPort.connections.FirstOrDefault();
                    if (next != null)
                    {
                        if (next.input.node is StoryCard storyTarget)
                        {
                            sc.Data.NextCardID = storyTarget.Data.StoryID;
                        }
                        else if (next.input.node is SubStoryCard subStoryTarget)
                        {
                            sc.Data.NextCardID = subStoryTarget.Data.CardID;
                        }
                        else if (next.input.node is BranchCard branchTarget)
                        {
                            sc.Data.NextCardID = branchTarget.Data.CardID;
                        }
                        else
                        {
                            sc.Data.NextCardID = "";
                        }
                    }
                    else
                    {
                        sc.Data.NextCardID = "";
                    }

                    sc.SaveToData();
                    break;

                case SubStoryCard ssc:
                    savedCount++;
                    
                    var subNext = ssc.OutputPort.connections.FirstOrDefault();
                    if (subNext != null)
                    {
                        // Only the target's card id is stored; its type is resolved at load time,
                        // so retyping a card does not invalidate every edge pointing at it.
                        if (subNext.input.node is StoryCard storyTarget)
                        {
                            ssc.Data.NextCardID = storyTarget.Data.StoryID;
                        }
                        else if (subNext.input.node is SubStoryCard subStoryTarget)
                        {
                            ssc.Data.NextCardID = subStoryTarget.Data.CardID;
                        }
                        else if (subNext.input.node is BranchCard branchTarget)
                        {
                            ssc.Data.NextCardID = branchTarget.Data.CardID;
                        }
                        else
                        {
                            ssc.Data.NextCardID = "";
                        }
                    }
                    else
                    {
                        // No edge means no successor.
                        ssc.Data.NextCardID = "";
                    }

                    ssc.SaveToData();
                    break;

                case BranchCard bc:
                    savedCount++;
                    
                    bc.SaveToData();

                    // A branch has several outputs, so its edges live in groups plus an else target.
                    bc.Data.groups.Clear();
                    foreach (var (port, conds) in bc.EnumerateGroups())
                        bc.Data.groups.Add(new BranchData.Group
                        {
                            conditions = new List<StoryBranchCondition>(conds),
                            targetID   = bc.TargetIDFromPort(port)
                        });
                    bc.Data.elseTargetID = bc.TargetIDFromPort(bc.ElsePort);
                    EditorUtility.SetDirty(bc.Data);
                    break;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[SaveGraph] Saved {savedCount} nodes with positions");
    }

    /* Rebuilding the graph from the ScriptableObjects. */
    public void LoadGraph(List<StoryData> stories, List<BranchData> branches, List<SubStoryData> subStories)
    {
        DeleteElements(graphElements.ToList());
        _cards.Clear();
        DisplayOrder.Clear();

        var subStoryCards = new Dictionary<string, SubStoryCard>();

        /* 1. Story cards. */
        foreach (var s in stories)
        {
            var card = new StoryCard(s);
            AddElement(card);
            card.LoadFromData();
            _cards[s.StoryID] = card;
            InjectMenuAndPing(card);

            if (s.NodePosition == Vector2.zero)
            {
                Debug.LogWarning($"[LoadGraph] Story '{s.StoryID}' has ZERO position!");
            }
        }

        /* 1b. Sub-story cards. */
        foreach (var ss in subStories)
        {
            if (string.IsNullOrEmpty(ss.CardID))
            {
                // A sub-story with no card id cannot be addressed by anything, so it gets no
                // node. Left on disk regardless: load stays read-only, and deleting an asset
                // because a window happened to open is not a decision this pass gets to make.
                Debug.LogWarning(
                    $"[StoryFlowEditor] Sub-story '{ss.name}' has no card id and is not shown. " +
                    "Delete it from the side menu if it is no longer wanted.");
                continue;
            }

            var card = new SubStoryCard(ss);
            AddElement(card);
            card.LoadFromData();
            subStoryCards[ss.CardID] = card;
            AttachSubStoryRename(card);
        }

        /* 2. Story outputs. Branch targets are deferred until the branch cards exist. */
        foreach (var s in stories)
        {
            if (!string.IsNullOrEmpty(s.NextCardID))
            {
                if (!_cards.TryGetValue(s.StoryID, out var from))
                {
                    Debug.LogError($"[LoadGraph]  Story '{s.StoryID}' not found in _cards!");
                    continue;
                }

                if (_cards.TryGetValue(s.NextCardID, out var toStory))
                {
                    AddElement(from.OutputPort.ConnectTo(toStory.InputPort));
                }
                else if (subStoryCards.TryGetValue(s.NextCardID, out var toSubStory))
                {
                    AddElement(from.OutputPort.ConnectTo(toSubStory.InputPort));
                }
                
            }
        }

        /* 2b. Sub-story outputs, same deferral. */
        foreach (var ss in subStories)
        {
            if (!string.IsNullOrEmpty(ss.NextCardID) && subStoryCards.TryGetValue(ss.CardID, out var from))
            {

                if (_cards.TryGetValue(ss.NextCardID, out var toStory))
                {
                    AddElement(from.OutputPort.ConnectTo(toStory.InputPort));
                }
                else if (subStoryCards.TryGetValue(ss.NextCardID, out var toSubStory))
                {
                    AddElement(from.OutputPort.ConnectTo(toSubStory.InputPort));
                }
                // Unresolved for now; the id is kept rather than cleared.
            }
        }

        /* 3. Branch cards, all created before any branch edge is restored. */
        var branchCards = new Dictionary<string, BranchCard>(); // cardID → BranchCard
        var branchCardsByData = new Dictionary<BranchData, BranchCard>();

        foreach (var bd in branches)
        {
            var bc = new BranchCard(bd);
            AddElement(bc);
            bc.LoadFromData();

            branchCardsByData[bd] = bc;

            branchCards[bd.CardID] = bc;
        }

        /* 4. Branch outputs. */
        foreach (var bd in branches)
        {
            
            if (!branchCardsByData.TryGetValue(bd, out var bc)) continue;

            foreach (var (g, idx) in bd.groups.Select((g, i) => (g, i)))
            {
                
                if (_cards.TryGetValue(g.targetID, out var storyTarget))
                {
                    AddElement(bc.EnumerateGroups().ElementAt(idx).port.ConnectTo(storyTarget.InputPort));
                }
                
                else if (branchCards.TryGetValue(g.targetID, out var branchTarget))
                {
                    AddElement(bc.EnumerateGroups().ElementAt(idx).port.ConnectTo(branchTarget.InputPort));
                }
            }

            // The else target.
            if (_cards.TryGetValue(bd.elseTargetID, out var storyElse))
            {
                AddElement(bc.ElsePort.ConnectTo(storyElse.InputPort));
            }
            else if (branchCards.TryGetValue(bd.elseTargetID, out var branchElse))
            {
                AddElement(bc.ElsePort.ConnectTo(branchElse.InputPort));
            }
        }

        /* 4b. The story and sub-story edges deferred in step 2. */
        foreach (var s in stories)
        {
            if (!string.IsNullOrEmpty(s.NextCardID) && _cards.TryGetValue(s.StoryID, out var from))
            {
                
                if (from.OutputPort.connections.Any())
                    continue;

                if (branchCards.TryGetValue(s.NextCardID, out var toBranch))
                {
                    AddElement(from.OutputPort.ConnectTo(toBranch.InputPort));
                }
                // A missing branch leaves NextCardID intact rather than silently clearing it.
            }
        }

        foreach (var ss in subStories)
        {
            if (!string.IsNullOrEmpty(ss.NextCardID) && subStoryCards.TryGetValue(ss.CardID, out var from))
            {
                
                if (from.OutputPort.connections.Any())
                    continue;

                if (branchCards.TryGetValue(ss.NextCardID, out var toBranch))
                {
                    AddElement(from.OutputPort.ConnectTo(toBranch.InputPort));
                }
                // A missing branch leaves NextCardID intact rather than silently clearing it.
            }
        }

        /* 5. Side-menu ordering. */
        DisplayOrder.AddRange(_cards.Values.OrderBy(c => c.Data.StoryID));
        DisplayOrder.AddRange(subStoryCards.Values.OrderBy(c => c.Data.CardID));
        DisplayOrder.AddRange(graphElements.OfType<BranchCard>().OrderBy(b => b.Data.name));

        // Load is deliberately read-only. An earlier version called SaveGraph() here to
        // normalise bad ids, and that wrote back node positions before they had all been
        // restored, losing the layout on every reload.
        Debug.Log($"[LoadGraph] Loaded {_cards.Count} stories, {subStoryCards.Count} substories, {branchCards.Count} branches");
    }

    /* Full backup and restore of every StoryData. */
    public void SaveFullBackup()
    {
        Debug.Log("[SaveFullBackup] Starting backup...");
        var storyCards = graphElements.OfType<StoryCard>().ToList();
        Debug.Log($"[SaveFullBackup] {storyCards.Count} story cards");

        if (storyCards.Count == 0)
        {
            EditorUtility.DisplayDialog("Backup Failed",
                "No story cards to back up. Load the graph first.",
                "OK");
            return;
        }

        string backupPath = StoryDataBackupUtility.CreateFullBackup(storyCards);
        Debug.Log($"[SaveFullBackup] Backup written: {backupPath}");

        EditorUtility.DisplayDialog("Full Backup Complete",
            $"Backed up every StoryData.\n\nPath: {backupPath}\n\n" +
            "Includes:\n" +
            "- Node positions\n" +
            "- NextCardID connections\n" +
            "- StoryName, Thumbnail, Color\n" +
            "- Clone information\n" +
            "- Memo and the remaining fields",
            "OK");
    }

    public void RestoreFromBackup()
    {
        var backups = StoryDataBackupUtility.GetAllBackups();
        if (backups == null || backups.Length == 0)
        {
            EditorUtility.DisplayDialog("No Backups",
                "No backup files found. Create one with Full Backup first.",
                "OK");
            return;
        }

        var backupNames = backups.Select(Path.GetFileName).ToArray();
        var popup = new GenericMenu();

        for (int i = 0; i < backups.Length; i++)
        {
            string backupPath = backups[i];
            string backupName = backupNames[i];
            popup.AddItem(new GUIContent(backupName), false, () =>
            {
                if (EditorUtility.DisplayDialog("Restore Backup",
                    $"Restore this backup?\n\n{backupName}\n\nThis overwrites the current data.",
                    "Yes", "Cancel"))
                {
                    int restoredCount = StoryDataBackupUtility.RestoreFromBackup(backupPath);
                    EditorUtility.DisplayDialog("Restore Complete",
                        $"Restore complete.\n\nStories restored: {restoredCount}",
                        "OK");

                    RequestReload?.Invoke();
                }
            });
        }

        popup.ShowAsContext();
    }

    /* Which ports may connect. */
    public override List<Port> GetCompatiblePorts(Port s, NodeAdapter _)
        => ports.Where(p => p.direction != s.direction && p.node != s.node).ToList();
}

#endif
