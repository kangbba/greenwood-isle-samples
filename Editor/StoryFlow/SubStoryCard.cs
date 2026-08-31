#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class SubStoryCard : BaseCard<SubStoryData>
{
    private VisualElement _candidateContainer;
    private readonly List<CandidateFieldUI> _candidateFields = new();
    private Button _addButton;

    class CandidateFieldUI
    {
        public VisualElement row;
        public TextField textField;
        public Label statusLabel;
        public VisualElement thumbnail;
    }

    public SubStoryCard(SubStoryData data)
    {
        Init(
            data,
            useInput: true,
            useOutput: true,
            size: new Vector2(400, 200),
            onSelect: () =>
            {
                EditorGUIUtility.PingObject(Data);
                Debug.Log($"[SubStoryCard] Selected: {Data.name}");
            },
            onDelete: () => Debug.Log($"[SubStoryCard] Deleted: {Data.name}")
        );

        string cardIdValue = string.IsNullOrEmpty(Data.cardID) ? "(empty)" : Data.cardID;
        title = $"<SubStory> {cardIdValue}";

        style.width = 450;
        style.minWidth = 450;
        style.maxWidth = 450;

        titleContainer.style.backgroundColor = new Color(0.5f, 0.2f, 0.7f, 1f);
        mainContainer.style.backgroundColor = new Color(0.5f, 0.2f, 0.7f, 0.08f);

        /* Flow id, read-only. */
        var cardIdContainer = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginLeft = 3,
                marginRight = 3,
                marginBottom = 4,
                paddingLeft = 4,
                paddingRight = 4,
                paddingTop = 4,
                paddingBottom = 4,
                backgroundColor = new Color(0.2f, 0.15f, 0.25f, 0.3f)
            }
        };
        var cardIdLabel = new Label("Card ID:")
        {
            style =
            {
                width = 70,
                unityFontStyleAndWeight = FontStyle.Bold,
                color = new Color(0.9f, 0.8f, 1f, 1f)
            }
        };
        var cardIdValueLabel = new Label(cardIdValue)
        {
            style =
            {
                flexGrow = 1,
                unityFontStyleAndWeight = FontStyle.Bold,
                color = new Color(0.9f, 0.9f, 0.9f, 1f),
                paddingLeft = 4
            }
        };
        cardIdContainer.Add(cardIdLabel);
        cardIdContainer.Add(cardIdValueLabel);
        mainContainer.Add(cardIdContainer);

        var candidateLabel = new Label("Candidate Story IDs (Manual Input)")
        {
            style =
            {
                marginTop = 8,
                marginBottom = 4,
                fontSize = 12,
                unityFontStyleAndWeight = FontStyle.Bold,
                color = new Color(0.9f, 0.8f, 1f, 1f)
            }
        };
        mainContainer.Add(candidateLabel);

        _candidateContainer = new VisualElement
        {
            style =
            {
                marginLeft = 4,
                marginRight = 4,
                paddingTop = 4,
                paddingBottom = 4,
                paddingLeft = 4,
                paddingRight = 4,
                backgroundColor = new Color(0.2f, 0.15f, 0.25f, 0.3f),
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new Color(0.5f, 0.3f, 0.7f, 0.5f),
                borderBottomColor = new Color(0.5f, 0.3f, 0.7f, 0.5f),
                borderLeftColor = new Color(0.5f, 0.3f, 0.7f, 0.5f),
                borderRightColor = new Color(0.5f, 0.3f, 0.7f, 0.5f),
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4
            }
        };

        mainContainer.Add(_candidateContainer);

        _addButton = new Button(() => AddCandidateField())
        {
            text = "+ Add Candidate Story ID",
            style = { marginTop = 2, marginBottom = 2 }
        };
        _addButton.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _addButton.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());

        _candidateContainer.Add(_addButton);

        CreateInitialCandidateFields();
        RefreshExpandedState();
    }

    private void AddCandidateField(string initialValue = "")
    {
        var fieldUI = new CandidateFieldUI();

        fieldUI.row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 2,
                marginLeft = 4,
                marginRight = 4,
                alignItems = Align.Center
            }
        };

        // TextField
        fieldUI.textField = new TextField
        {
            value = initialValue,
            style =
            {
                flexGrow = 1,
                minWidth = 150
            }
        };
        fieldUI.textField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        fieldUI.textField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        fieldUI.textField.RegisterValueChangedCallback(evt => UpdateFieldStatus(fieldUI));

        fieldUI.row.Add(fieldUI.textField);

        // Status label: OK or MISSING
        fieldUI.statusLabel = new Label("")
        {
            style =
            {
                width = 20,
                unityTextAlign = TextAnchor.MiddleCenter,
                fontSize = 14,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginLeft = 4,
                marginRight = 4
            }
        };
        fieldUI.row.Add(fieldUI.statusLabel);

        // Thumbnail
        fieldUI.thumbnail = new VisualElement
        {
            style =
            {
                width = 40,
                height = 40,
                marginLeft = 4,
                marginRight = 4,
                backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f),
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new Color(0.3f, 0.3f, 0.3f, 1f),
                borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 1f),
                borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 1f),
                borderRightColor = new Color(0.3f, 0.3f, 0.3f, 1f)
            }
        };
        fieldUI.row.Add(fieldUI.thumbnail);

        // Remove Button
        var removeButton = new Button(() => RemoveCandidateField(fieldUI))
        {
            text = "X",
            style = { width = 25 }
        };
        removeButton.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        removeButton.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        fieldUI.row.Add(removeButton);

        int insertIndex = Mathf.Max(0, _candidateContainer.childCount - 1);
        _candidateContainer.Insert(insertIndex, fieldUI.row);

        _candidateFields.Add(fieldUI);

        UpdateFieldStatus(fieldUI);
    }

    private void UpdateFieldStatus(CandidateFieldUI fieldUI)
    {
        string storyId = fieldUI.textField.value?.Trim();

        if (string.IsNullOrEmpty(storyId))
        {
            fieldUI.statusLabel.text = "";
            fieldUI.statusLabel.style.color = Color.gray;
            fieldUI.thumbnail.style.backgroundImage = null;
            return;
        }

        StoryData storyData = FindStoryData(storyId);

        if (storyData != null)
        {
            
            fieldUI.statusLabel.text = "OK";
            fieldUI.statusLabel.style.color = new Color(0.3f, 1f, 0.3f, 1f);

            if (storyData.StoryThumbnail != null)
            {
                fieldUI.thumbnail.style.backgroundImage = new StyleBackground(storyData.StoryThumbnail);
            }
            else
            {
                fieldUI.thumbnail.style.backgroundImage = null;
            }
        }
        else
        {
            
            fieldUI.statusLabel.text = "MISSING";
            fieldUI.statusLabel.style.color = new Color(1f, 0.3f, 0.3f, 1f); 
            fieldUI.thumbnail.style.backgroundImage = null;
        }
    }

    private StoryData FindStoryData(string storyId)
    {
        
        string path = $"Stories/StoryDatas/{storyId}";
        StoryData data = Resources.Load<StoryData>(path);
        return data;
    }

    private void RemoveCandidateField(CandidateFieldUI fieldUI)
    {
        if (_candidateFields.Count <= 1) return; // Always keep one row.

        _candidateFields.Remove(fieldUI);
        if (fieldUI.row != null && fieldUI.row.parent != null)
            fieldUI.row.parent.Remove(fieldUI.row);

        MarkDirtyRepaint();
    }

    private void CreateInitialCandidateFields()
    {
        if (Data.candidateStoryIds != null && Data.candidateStoryIds.Count > 0)
        {
            foreach (var storyId in Data.candidateStoryIds)
            {
                AddCandidateField(storyId);
            }
        }
        else
        {
            
            AddCandidateField("");
        }
    }

    public override void LoadFromData()
    {
        SetPosition(new Rect(Data.NodePosition, new Vector2(450, 200)));
    }

    public override void SaveToData()
    {
        Data.NodePosition = GetPosition().position;

        var candidates = new List<string>();
        foreach (var fieldUI in _candidateFields)
        {
            string value = fieldUI.textField.value?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                candidates.Add(value);
            }
        }
        Data.candidateStoryIds = candidates;

        EditorUtility.SetDirty(Data);
    }
}
#endif
