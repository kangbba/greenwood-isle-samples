#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class BranchCard : BaseCard<BranchData>
{
    public Port ElsePort;

    class CondUI
    {
        public StoryBranchCondition cond;
        public VisualElement row;

        public DropdownField key;      // Ordinary condition kinds.
        public DropdownField cmp;
        public IntegerField  val;

        public EnumField beliefType;   // BeliefValue only.
        public EnumField beliefSide;
    }

    class GroupUI
    {
        public Port port;
        public VisualElement box;
        public Label headLabel;
        public List<CondUI> conds = new();
    }

    readonly List<GroupUI> _groups = new();

    static readonly string[] CharKeys   = GetConstStrings(typeof(CNames));
    static readonly string[] ItemKeys   = GetConstStrings(typeof(ItemNames));
    static readonly string[] ChoiceKeys = GetConstStrings(typeof(ChoiceFlags));

    static string[] GetConstStrings(System.Type t) =>
        t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
         .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
         .Select(f => (string)f.GetRawConstantValue())
         .OrderBy(s => s)
         .ToArray();

    public BranchCard(BranchData data)
    {
        title = $"<Branch> {data.name}";
        titleContainer.style.backgroundColor = new Color(0.1f, 0.55f, 0.55f, 1f);
        mainContainer.style.backgroundColor  = new Color(0.1f, 0.55f, 0.55f, 0.08f);

        Init(
            data,
            useInput: true,
            useOutput: false,
            size: new Vector2(400, 190),
            onSelect: () => EditorGUIUtility.PingObject(Data),
            onDelete: () => Debug.Log($"[BranchCard] Deleted: {Data.name}")
        );

        style.width = 600;
        style.minWidth = 600;
        style.maxWidth = 600;

        var idRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
        idRow.Add(new Label("Branch ID:") { style = { width = 80, unityFontStyleAndWeight = FontStyle.Bold } });
        var idField = new TextField { value = data.name, style = { flexGrow = 1 } };
        idField.RegisterValueChangedCallback(e =>
        {
            if (!string.IsNullOrWhiteSpace(e.newValue) && e.newValue != data.name)
            {
                // 1. Rename the asset, which is the card id.
                string oldPath = AssetDatabase.GetAssetPath(data);
                AssetDatabase.RenameAsset(oldPath, e.newValue);
                AssetDatabase.SaveAssets();

                // 2. Reload the whole graph so every reference is rebuilt.
                var graphView = this.GetFirstAncestorOfType<StoryFlowGraphView>();
                graphView?.RequestReload?.Invoke();

                Debug.Log($"[BranchCard] Renamed {data.name} to {e.newValue}.");
            }
        });
        idRow.Add(idField);
        mainContainer.Insert(0, idRow);

        ElsePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        ElsePort.portName = "else";
        outputContainer.Add(ElsePort);

        var fold = new Foldout { text = "Groups", value = true };
        extensionContainer.Add(fold);
        fold.Add(new Button(() => { AddGroupUI(fold); RefreshGraph(); }) { text = "+ Add Group" });

        if (data.groups != null && data.groups.Count > 0)
            foreach (var g in data.groups) AddGroupUI(fold, g);

        PruneEmptyGroups();
        RenumberGroups();
        RefreshAllPortLabels();
        RefreshGraph();
    }

    void AddGroupUI(Foldout parent, BranchData.Group preset = null)
    {
        var g = new GroupUI();
        g.port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        outputContainer.Insert(_groups.Count, g.port);

        g.box = new Box { style = { marginTop = 3, paddingLeft = 4 } };
        var head = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        g.headLabel = new Label() { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        head.Add(g.headLabel);
        head.Add(new Button(() => { AddCondUI(g); RefreshGraph(); }) { text = "+ Cond" });
        head.Add(new Button(() => { RemoveGroupUI(g); RefreshGraph(); }) { text = "Del Group" });
        g.box.Add(head);
        parent.Add(g.box);
        _groups.Add(g);

        if (preset != null && preset.conditions != null && preset.conditions.Count > 0)
            foreach (var c in preset.conditions) AddCondUI(g, c);

        UpdateGroupPortLabel(g);
        RenumberGroups();
    }

    void AddCondUI(GroupUI g, StoryBranchCondition preset = null)
    {
        var c = preset ?? new StoryBranchCondition
        {
            kind        = ConditionKind.Affinity,
            key         = CharKeys.FirstOrDefault() ?? "",
            compareType = CompareType.GreaterOrEqual,
            threshold   = 50,
            beliefType  = BeliefType.Time,
            beliefSide  = BeliefSide.Left
        };

        c.compareType = ConditionRules.Normalize(c.kind, c.compareType);

        var ui  = new CondUI { cond = c };
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 8 } };
        ui.row  = row;

        // kind
        var kindF = new EnumField(c.kind);
        kindF.RegisterValueChangedCallback(e =>
        {
            c.kind = (ConditionKind)e.newValue;
            c.compareType = ConditionRules.Normalize(c.kind, c.compareType);

            UpdateKeyChoices(ui);
            RebuildCmp(ui);
            UpdateVisibility(ui);

            UpdateGroupPortLabel(g);
            RefreshGraph();
        });
        row.Add(kindF);

        var keyChoices = GetKeyList(c.kind);
        if (keyChoices.Count == 0) keyChoices = new List<string> { "" }; // DropdownField needs at least one entry.
        if (!keyChoices.Contains(c.key)) c.key = keyChoices[0];
        ui.key = new DropdownField(keyChoices, c.key) { style = { flexGrow = 1 } };
        ui.key.RegisterValueChangedCallback(e =>
        {
            c.key = e.newValue;
            UpdateGroupPortLabel(g);
            RefreshGraph();
        });
        row.Add(ui.key);

        // cmp
        ui.cmp = BuildCmpDropdown(c.kind, c.compareType, ct => c.compareType = ct, () =>
        {
            UpdateGroupPortLabel(g);
            RefreshGraph();
        });
        row.Add(ui.cmp);

        // val
        ui.val = new IntegerField { value = c.threshold, style = { width = 55 } };
        ui.val.RegisterValueChangedCallback(e =>
        {
            c.threshold = e.newValue;
            UpdateGroupPortLabel(g);
            RefreshGraph();
        });
        row.Add(ui.val);

        ui.beliefType = new EnumField(c.beliefType) { style = { width = 110 } };
        ui.beliefType.RegisterValueChangedCallback(e =>
        {
            c.beliefType = (BeliefType)e.newValue;
            UpdateGroupPortLabel(g);
            RefreshGraph();
        });
        row.Add(ui.beliefType);

        ui.beliefSide = new EnumField(c.beliefSide) { style = { width = 90 } };
        ui.beliefSide.RegisterValueChangedCallback(e =>
        {
            c.beliefSide = (BeliefSide)e.newValue;
            UpdateGroupPortLabel(g);
            RefreshGraph();
        });
        row.Add(ui.beliefSide);

        // X
        row.Add(new Button(() =>
        {
            g.box.Remove(row);
            g.conds.Remove(ui);
            if (g.conds.Count == 0) RemoveGroupUI(g);
            else UpdateGroupPortLabel(g);
            RefreshGraph();
        })
        { text = "X", style = { width = 20 } });

        g.box.Add(row);
        g.conds.Add(ui);

        UpdateKeyChoices(ui);
        UpdateVisibility(ui);
        UpdateGroupPortLabel(g);
    }

    List<string> GetKeyList(ConditionKind kind) => kind switch
    {
        ConditionKind.Affinity                   => CharKeys.ToList(),
        ConditionKind.HasItem                    => ItemKeys.ToList(),
        ConditionKind.IsHighestAffinityCharacter => CharKeys.ToList(),
        ConditionKind.IsLowestAffinityCharacter  => CharKeys.ToList(),
        ConditionKind.SelectedChoice             => ChoiceKeys.ToList(),
        ConditionKind.BeliefValue                => new List<string>(), // Uses the enum fields instead.
        _                                        => CharKeys.ToList()
    };

    void UpdateKeyChoices(CondUI ui)
    {
        var c = ui.cond;
        var list = GetKeyList(c.kind);

        if (list.Count == 0)
            list = new List<string> { "" };

        if (string.IsNullOrEmpty(c.key) || !list.Contains(c.key))
            c.key = list[0];

        ui.key.choices = list;
        ui.key.value = c.key;
    }

    DropdownField BuildCmpDropdown(ConditionKind kind, CompareType current, System.Action<CompareType> set, System.Action onChanged)
    {
        var allowed = ConditionRules.AllowedCompareTypes(kind);
        var labels  = allowed.Select(a => a.ToString()).ToList();

        var fixedCurrent = ConditionRules.Normalize(kind, current);
        int idx = Mathf.Max(0, labels.IndexOf(fixedCurrent.ToString()));

        var dd = new DropdownField(labels, idx) { style = { width = 95 } };
        dd.RegisterValueChangedCallback(e =>
        {
            if (System.Enum.TryParse<CompareType>(e.newValue, out var parsed))
                set(parsed);
            else if (allowed.Length > 0)
                set(allowed[0]);

            onChanged?.Invoke();
        });

        return dd;
    }

    void RebuildCmp(CondUI ui)
    {
        var c = ui.cond;
        int idx = ui.row.IndexOf(ui.cmp);
        ui.row.Remove(ui.cmp);

        c.compareType = ConditionRules.Normalize(c.kind, c.compareType);
        ui.cmp = new DropdownField(
            ConditionRules.AllowedCompareTypes(c.kind).Select(x => x.ToString()).ToList(),
            0
        )
        { style = { width = 95 } };

        var allowed = ConditionRules.AllowedCompareTypes(c.kind);
        var labels  = allowed.Select(a => a.ToString()).ToList();
        int curIdx  = Mathf.Max(0, labels.IndexOf(c.compareType.ToString()));
        ui.cmp.index = curIdx;

        ui.cmp.RegisterValueChangedCallback(e =>
        {
            if (System.Enum.TryParse<CompareType>(e.newValue, out var parsed))
                c.compareType = parsed;
            else if (allowed.Length > 0)
                c.compareType = allowed[0];
        });

        ui.row.Insert(idx, ui.cmp);
    }

    void UpdateVisibility(CondUI ui)
    {
        var c = ui.cond;

        bool needsNum    = ConditionRules.NeedsNumericValue(c.kind);
        bool needsBelief = ConditionRules.NeedsBeliefEnum(c.kind);

        ui.key.style.display = (!needsBelief && ConditionRules.NeedsKey(c.kind)) ? DisplayStyle.Flex : DisplayStyle.None;
        ui.cmp.style.display = (!needsBelief && ConditionRules.AllowedCompareTypes(c.kind).Length > 0) ? DisplayStyle.Flex : DisplayStyle.None;
        ui.val.style.display = (!needsBelief && needsNum) ? DisplayStyle.Flex : DisplayStyle.None;

        ui.beliefType.style.display = needsBelief ? DisplayStyle.Flex : DisplayStyle.None;
        ui.beliefSide.style.display = needsBelief ? DisplayStyle.Flex : DisplayStyle.None;

        if (!needsNum) { ui.val.value = 0; c.threshold = 0; }
        if (needsBelief && c.compareType != CompareType.Equal && c.compareType != CompareType.NotEqual)
            c.compareType = CompareType.Equal;
    }

    void RemoveGroupUI(GroupUI g)
    {
        var gv = this.GetFirstAncestorOfType<GraphView>();
        if (g.port != null)
        {
            var edges = g.port.connections?.ToList();
            if (edges != null && gv != null) gv.DeleteElements(edges);
        }
        if (g.box != null && g.box.parent != null) g.box.parent.Remove(g.box);
        if (g.port != null && g.port.parent != null) g.port.parent.Remove(g.port);
        _groups.Remove(g);
        RenumberGroups();
        RefreshAllPortLabels();
    }

    void UpdateGroupPortLabel(GroupUI g)
    {
        if (g == null || g.port == null) return;
        if (g.conds.Count == 0) { g.port.portName = ""; return; }

        g.port.portName = string.Join(" && ",
            g.conds.Select(cu =>
            {
                string label = BranchEvaluator.ToLabelString(cu.cond);
                var connected = g.port.connections.FirstOrDefault()?.input?.node;
                if (connected is StoryCard sc) label += $" → {sc.Data.DisplayName}";
                return label;
            }));
    }

    void RefreshAllPortLabels()
    {
        foreach (var grp in _groups) UpdateGroupPortLabel(grp);
    }

    void RenumberGroups()
    {
        for (int i = 0; i < _groups.Count; i++)
        {
            var g = _groups[i];
            if (g.headLabel != null) g.headLabel.text = $"Group {i + 1}";
            if (g.port != null && g.port.parent == outputContainer)
            {
                int maxIndex = outputContainer.IndexOf(ElsePort);
                outputContainer.Remove(g.port);
                outputContainer.Insert(Mathf.Clamp(i, 0, maxIndex), g.port);
            }
        }
    }

    void PruneEmptyGroups()
    {
        for (int i = _groups.Count - 1; i >= 0; i--)
            if (_groups[i].conds == null || _groups[i].conds.Count == 0)
                RemoveGroupUI(_groups[i]);
    }

    void RefreshGraph()
    {
        RefreshAllPortLabels();
        RefreshPorts();
        RefreshExpandedState();
        MarkDirtyRepaint();
        outputContainer.MarkDirtyRepaint();
        extensionContainer.MarkDirtyRepaint();
    }

    public IEnumerable<(Port port, List<StoryBranchCondition>)> EnumerateGroups() =>
        _groups.Select(grp => (grp.port, grp.conds.Select(cu => cu.cond).ToList()));

    public string TargetIDFromPort(Port p)
    {
        var targetNode = p?.connections.FirstOrDefault()?.input.node;
        return targetNode switch
        {
            StoryCard sc => sc.Data.CardID,
            BranchCard bc => bc.Data.CardID,
            _ => ""
        };
    }

    public override void LoadFromData() =>
        SetPosition(new Rect(Data.NodePosition, new Vector2(320, 190)));

    public override void SaveToData()
    {
        Data.NodePosition = GetPosition().position;
        EditorUtility.SetDirty(Data);
    }
}
#endif
