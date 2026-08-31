#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class StoryCard : BaseCard<StoryData>
{
    private readonly Image _thumbPreview;
    private TextField _nameField;
    private ColorField _colorField;
    private ObjectField _thumbField;
    private IntegerField _buildField;
    private Toggle _summaryToggle;
    private StoryCardSummary _summary;
    private Toggle _deadEndToggle;
    private Toggle _memoToggle;
    private VisualElement _memoPanel;
    private TextField _memoField;
    private Button _playButton;
    private Label _playStoryIdValue;

    // A clone shows the original's values as read-only labels in place of the
    // editable fields, so the two cannot drift apart.
    private VisualElement _cloneNameContainer;
    private Label _cloneNameLabel;
    private VisualElement _cloneThumbnailContainer;
    private Label _cloneThumbnailLabel;
    private VisualElement _cloneColorContainer;
    private Label _cloneColorLabel;
    private VisualElement _cloneBuildContainer;
    private Label _cloneBuildLabel;

    private VisualElement _cloneFieldsContainer;
    private TextField _originalStoryIDField;
    private TextField _cloneSuffixField;
    private Label _cloneWarningLabel;

    private IVisualElementScheduledItem _scheduledUpdate;

    private (VisualElement container, Label label) CreateCloneReadOnlyField(string labelText)
    {
        var container = new VisualElement { style = { display = DisplayStyle.None } };

        // Styled to match Unity's own disabled fields, so a clone reads as locked
        // rather than broken.
        var titleLabel = new Label(labelText)
        {
            style =
            {
                marginBottom = 2,
                fontSize = 12,
                unityFontStyleAndWeight = FontStyle.Normal,
                color = new Color(0.7f, 0.7f, 0.7f, 1f), 
                marginTop = 2
            }
        };
        container.Add(titleLabel);

        var valueLabel = new Label()
        {
            style =
            {
                
                marginLeft = 3,
                marginRight = 3,
                marginTop = 1,
                marginBottom = 2,
                paddingLeft = 3,
                paddingRight = 3,
                paddingTop = 2,
                paddingBottom = 2,

                backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f),

                color = new Color(0.5f, 0.5f, 0.5f, 1f),

                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderBottomColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderLeftColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderRightColor = new Color(0.13f, 0.13f, 0.13f, 1f),

                borderTopLeftRadius = 3,
                borderTopRightRadius = 3,
                borderBottomLeftRadius = 3,
                borderBottomRightRadius = 3,

                whiteSpace = WhiteSpace.Normal,
                minHeight = 20,
                fontSize = 12
            }
        };
        container.Add(valueLabel);
        return (container, valueLabel);
    }

    public StoryCard(StoryData data)
    {
        Init(
            data,
            useInput  : true,
            useOutput : true,
            size      : StoryCardUI.CardSize,
            onSelect  : () =>
            {
                EditorGUIUtility.PingObject(Data);
                Debug.Log($"[StoryCard] Selected: {Data.name}");
            },
            onDelete  : () => Debug.Log($"[StoryCard] Deleted: {Data.name}")
        );

        title = string.Format(StoryCardUI.TitleFormat, Data.StoryName_KO, Data.StoryID);

        style.width = StoryCardUI.CardSize.x;
        style.minWidth = StoryCardUI.CardSize.x;
        style.maxWidth = StoryCardUI.CardSize.x;

        titleContainer.style.backgroundColor = StoryCardUI.TitleBackground;

        /* Clone toggle and fields. */
        CreateCloneSection();

        /* Story name, editable. */
        _nameField = new TextField("Story Name (KO)")
        {
            value = Data.StoryName_KO,
            multiline = true,
        };
        _nameField.style.whiteSpace = WhiteSpace.Normal;
        _nameField.RegisterValueChangedCallback(e =>
        {
            // Ignored on a clone; the original owns this value.
            if (Data.IsClone)
            {
                e.StopPropagation();
                return;
            }

            Data.StoryName_KO = e.newValue;
            title = string.Format(StoryCardUI.TitleFormat, Data.StoryName_KO, Data.StoryID);
            EditorUtility.SetDirty(Data);
        });
        // Stopped so typing in the field does not also select the node.
        _nameField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _nameField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        mainContainer.Add(_nameField);

        /* Card id, read-only: it is the asset name. */
        var cardIdLabel = new Label("Card ID")
        {
            style =
            {
                marginBottom = 2,
                fontSize = 11,
                unityFontStyleAndWeight = FontStyle.Normal,
                color = new Color(0.7f, 0.7f, 0.7f, 1f)
            }
        };
        mainContainer.Add(cardIdLabel);

        var cardIdValue = new Label(Data.CardID)
        {
            style =
            {
                marginLeft = 3,
                marginRight = 3,
                marginBottom = 4,
                paddingLeft = 3,
                paddingRight = 3,
                paddingTop = 2,
                paddingBottom = 2,
                backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f),
                color = new Color(0.5f, 0.5f, 0.5f, 1f),
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderBottomColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderLeftColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderRightColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderTopLeftRadius = 3,
                borderTopRightRadius = 3,
                borderBottomLeftRadius = 3,
                borderBottomRightRadius = 3,
                minHeight = 18,
                fontSize = 12
            }
        };
        mainContainer.Add(cardIdValue);

        /* Play story id, read-only. */
        var playStoryIdLabel = new Label("Play Story ID")
        {
            style =
            {
                marginBottom = 2,
                fontSize = 11,
                unityFontStyleAndWeight = FontStyle.Normal,
                color = new Color(0.7f, 0.7f, 0.7f, 1f)
            }
        };
        mainContainer.Add(playStoryIdLabel);

        _playStoryIdValue = new Label(Data.StoryIDToPlay)
        {
            style =
            {
                marginLeft = 3,
                marginRight = 3,
                marginBottom = 4,
                paddingLeft = 3,
                paddingRight = 3,
                paddingTop = 2,
                paddingBottom = 2,
                backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f),
                color = new Color(0.5f, 0.5f, 0.5f, 1f),
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderBottomColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderLeftColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderRightColor = new Color(0.13f, 0.13f, 0.13f, 1f),
                borderTopLeftRadius = 3,
                borderTopRightRadius = 3,
                borderBottomLeftRadius = 3,
                borderBottomRightRadius = 3,
                minHeight = 18,
                fontSize = 12
            }
        };
        mainContainer.Add(_playStoryIdValue);

        /* Story name, read-only clone view. */
        (_cloneNameContainer, _cloneNameLabel) = CreateCloneReadOnlyField("Story Name (KO)");
        mainContainer.Add(_cloneNameContainer);
        
        float thumbHeight = StoryCardUI.CardSize.x / StoryCardUI.ThumbAspectW * StoryCardUI.ThumbAspectH;
        _thumbPreview = new Image
        {
            image     = Data.StoryThumbnail ? Data.StoryThumbnail.texture : null,
            tintColor = data.StoryColor,
            scaleMode = ScaleMode.ScaleToFit,
            style =
            {
                width           = StoryCardUI.CardSize.x - 20,
                height          = thumbHeight,
                alignSelf       = Align.Center,
                marginTop       = StoryCardUI.ThumbMarginTop,
                backgroundColor = StoryCardUI.ThumbBack
            }
        };
        mainContainer.Add(_thumbPreview);

        _thumbField = new ObjectField("Thumbnail")
        {
            objectType = typeof(Sprite),
            value      = Data.StoryThumbnail
        };
        _thumbField.RegisterValueChangedCallback(e =>
        {
            // Ignored on a clone; the original owns this value.
            if (Data.IsClone)
            {
                e.StopPropagation();
                return;
            }

            Data.StoryThumbnail = e.newValue as Sprite;
            _thumbPreview.image = Data.StoryThumbnail ? Data.StoryThumbnail.texture : null;
            EditorUtility.SetDirty(Data);
        });
        // Stopped so typing in the field does not also select the node.
        _thumbField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _thumbField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        mainContainer.Add(_thumbField);

        (_cloneThumbnailContainer, _cloneThumbnailLabel) = CreateCloneReadOnlyField("Thumbnail");
        mainContainer.Add(_cloneThumbnailContainer);

        _colorField = new ColorField("Thumbnail Color")
        {
            value = Data.StoryColor,
        };
        _colorField.RegisterValueChangedCallback(e =>
        {
            // Ignored on a clone; the original owns this value.
            if (Data.IsClone)
            {
                e.StopPropagation();
                return;
            }

            Data.StoryColor = e.newValue;

            if (_thumbPreview != null)
                _thumbPreview.tintColor = e.newValue;

            EditorUtility.SetDirty(Data);
        });
        // Stopped so typing in the field does not also select the node.
        _colorField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _colorField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        mainContainer.Add(_colorField);

        (_cloneColorContainer, _cloneColorLabel) = CreateCloneReadOnlyField("Thumbnail Color");
        mainContainer.Add(_cloneColorContainer);

        _buildField = new IntegerField("Build Version") { value = Data.StoryBuildVersion };
        _buildField.RegisterValueChangedCallback(e =>
        {
            // Ignored on a clone; the original owns this value.
            if (Data.IsClone)
            {
                e.StopPropagation();
                return;
            }

            Data.StoryBuildVersion = e.newValue;
            EditorUtility.SetDirty(Data);
        });
        // Stopped so typing in the field does not also select the node.
        _buildField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _buildField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        mainContainer.Add(_buildField);

        (_cloneBuildContainer, _cloneBuildLabel) = CreateCloneReadOnlyField("Build Version");
        mainContainer.Add(_cloneBuildContainer);

        _deadEndToggle = new Toggle("Is Dead End") { value = Data.IsDeadEnd };
        _deadEndToggle.RegisterValueChangedCallback(e =>
        {
            Data.IsDeadEnd = e.newValue;
            ApplyStatusStyling();
            EditorUtility.SetDirty(Data);
        });
        _deadEndToggle.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _deadEndToggle.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        mainContainer.Add(_deadEndToggle);

        /* Play: starts a new game from this card, using the same default save the
           game itself builds, so the editor path and the shipped path agree. */
        _playButton = new Button(() =>
        {
            
            var save = SaveLoadManager.Instance.CreateNewSaveData();
            if (save == null)
            {
                Debug.LogError("[StoryCard] Could not build a default SaveData.");
                return;
            }

            save.currentCardID = Data.CardID;
            save.currentElementIndex = 0;

            if (InGameManager.HasInstance)
            {
                Debug.Log($"[StoryCard] Already in game; restarting at '{Data.StoryID}'.");
                GameManager.Instance.RestartInGame(save);
            }
            else
            {
                Debug.Log($"[StoryCard] Starting a new game at '{Data.StoryID}'.");
                GameManager.Instance.StartInGame(save);
            }
        })
        {
            text = "Play",
            style = { marginTop = 4 }
        };

        _playButton.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _playButton.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());

        mainContainer.Add(_playButton);

        _summaryToggle = new Toggle("Show Summary") { value = Data.ShowSummary };
        _summaryToggle.RegisterValueChangedCallback(e =>
        {
            Data.ShowSummary = e.newValue;
            EditorUtility.SetDirty(Data);
            if (_summary != null)
                _summary.UpdateVisibility();
        });
        _summaryToggle.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _summaryToggle.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        mainContainer.Add(_summaryToggle);

        _summary = new StoryCardSummary(Data, this);
        var summaryPanel = _summary.CreateSummaryPanel();
        this.Add(summaryPanel);

        _memoToggle = new Toggle("Use Memo") { value = Data.UseMemo };
        _memoToggle.RegisterValueChangedCallback(e =>
        {
            Data.UseMemo = e.newValue;
            EditorUtility.SetDirty(Data);
            UpdateMemoVisibility();
        });
        _memoToggle.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _memoToggle.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        mainContainer.Add(_memoToggle);

        CreateMemoPanel();

        ApplyStatusStyling();
        RefreshPorts();
        RefreshExpandedState();

        SetupEditorUpdateCallback();
    }

    private void SetupEditorUpdateCallback()
    {
        
        _scheduledUpdate?.Pause();
        _scheduledUpdate = null;

        // Only a clone polls, and only to mirror the original's fields.
        if (Data.IsClone)
        {
            _scheduledUpdate = this.schedule.Execute(() =>
            {
                if (Data != null && Data.IsClone)
                {
                    UpdateCloneInfo();
                }
            }).Every(500);
        }
    }

    private void CreateCloneSection()
    {
        // Clones are created through the context menu only, and the flag is one-way:
        // un-cloning would leave the copied fields with no owner. The toggle is display-only.
        var isCloneToggle = new Toggle("Is Clone") { value = Data.IsClone };
        isCloneToggle.SetEnabled(false);
        isCloneToggle.tooltip = "Clone can only be created via right-click menu. Cannot be toggled manually.";
        mainContainer.Add(isCloneToggle);

        _cloneFieldsContainer = new VisualElement
        {
            style =
            {
                display = Data.IsClone ? DisplayStyle.Flex : DisplayStyle.None,
                marginTop = 4,
                marginBottom = 4,
                paddingTop = 4,
                paddingBottom = 4,
                paddingLeft = 4,
                paddingRight = 4,
                backgroundColor = new Color(0.2f, 0.3f, 0.5f, 0.2f),
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopColor = new Color(0.3f, 0.5f, 1f, 0.5f),
                borderBottomColor = new Color(0.3f, 0.5f, 1f, 0.5f),
                borderLeftColor = new Color(0.3f, 0.5f, 1f, 0.5f),
                borderRightColor = new Color(0.3f, 0.5f, 1f, 0.5f)
            }
        };

        _originalStoryIDField = new TextField("Original Story ID")
        {
            value = Data.OriginalStoryID
        };
        _originalStoryIDField.RegisterValueChangedCallback(e =>
        {
            Data.OriginalStoryID = e.newValue;
            EditorUtility.SetDirty(Data);
            UpdateCloneInfo();
        });
        // Stopped so typing in the field does not also select the node.
        _originalStoryIDField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _originalStoryIDField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        _cloneFieldsContainer.Add(_originalStoryIDField);

        _cloneSuffixField = new TextField("Clone Suffix")
        {
            value = Data.CloneSuffix
        };
        _cloneSuffixField.RegisterValueChangedCallback(e =>
        {
            Data.CloneSuffix = e.newValue;
            EditorUtility.SetDirty(Data);
            UpdateCloneInfo();
        });
        // Stopped so typing in the field does not also select the node.
        _cloneSuffixField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _cloneSuffixField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());
        _cloneFieldsContainer.Add(_cloneSuffixField);

        _cloneWarningLabel = new Label("")
        {
            style =
            {
                marginTop = 4,
                whiteSpace = WhiteSpace.Normal,
                color = new Color(1f, 1f, 0.5f, 1f)
            }
        };
        _cloneFieldsContainer.Add(_cloneWarningLabel);

        mainContainer.Add(_cloneFieldsContainer);

        UpdateCloneVisibility();
        UpdateCloneInfo();
    }

    private void UpdateCloneVisibility()
    {
        if (_cloneFieldsContainer == null) return;
        _cloneFieldsContainer.style.display = Data.IsClone ? DisplayStyle.Flex : DisplayStyle.None;

        // Editable fields and read-only labels are the same information in two forms;
        // exactly one set is visible at a time.
        bool isClone = Data.IsClone;
        var editDisplay = isClone ? DisplayStyle.None : DisplayStyle.Flex;
        var readonlyDisplay = isClone ? DisplayStyle.Flex : DisplayStyle.None;

        // StoryName_KO
        if (_nameField != null)
        {
            _nameField.style.display = editDisplay;
            _nameField.SetEnabled(!isClone);
        }
        if (_cloneNameContainer != null) _cloneNameContainer.style.display = readonlyDisplay;

        // Thumbnail
        if (_thumbField != null) _thumbField.style.display = editDisplay;
        if (_cloneThumbnailContainer != null) _cloneThumbnailContainer.style.display = readonlyDisplay;

        // Color
        if (_colorField != null) _colorField.style.display = editDisplay;
        if (_cloneColorContainer != null) _cloneColorContainer.style.display = readonlyDisplay;

        // BuildVersion
        if (_buildField != null) _buildField.style.display = editDisplay;
        if (_cloneBuildContainer != null) _cloneBuildContainer.style.display = readonlyDisplay;

        if (_playButton != null) _playButton.style.display = editDisplay;

        // Kept enabled on a clone: reading the original's summary is still useful.
        if (_summaryToggle != null) _summaryToggle.SetEnabled(!isClone);

        if (_memoToggle != null) _memoToggle.SetEnabled(!isClone);

        if (_memoField != null) _memoField.SetEnabled(!isClone);
    }

    private void UpdateCloneInfo()
    {
        if (!Data.IsClone || _cloneWarningLabel == null) return;

        RefreshPlayStoryIdLabel();

        var originalPath = $"Assets/Resources/Stories/StoryDatas/{Data.OriginalStoryID}.asset";
        var original = AssetDatabase.LoadAssetAtPath<StoryData>(originalPath);

        if (original != null)
        {
            if (Data.StoryName_KO != original.StoryName_KO)
            {
                Data.StoryName_KO = original.StoryName_KO;
                if (_nameField != null)
                    _nameField.value = Data.StoryName_KO;
                EditorUtility.SetDirty(Data);
            }

            _cloneWarningLabel.text = $"Original found: {original.StoryName_KO}\nFull clone ID: {Data.StoryID}";
            _cloneWarningLabel.style.color = new Color(0.5f, 1f, 0.5f, 1f);

            title = string.Format(StoryCardUI.TitleFormat, Data.StoryName_KO, Data.StoryID);

            if (_cloneNameLabel != null)
                _cloneNameLabel.text = original.StoryName_KO;

            if (_thumbPreview != null)
            {
                _thumbPreview.image = original.StoryThumbnail ? original.StoryThumbnail.texture : null;
                _thumbPreview.tintColor = original.StoryColor;
                _thumbPreview.style.opacity = 0.7f; // Dimmed, so a clone is identifiable at a glance.
            }

            if (_cloneThumbnailLabel != null)
                _cloneThumbnailLabel.text = original.StoryThumbnail != null ? original.StoryThumbnail.name : "(None)";

            if (_cloneColorLabel != null)
            {
                _cloneColorLabel.text = $"RGB({original.StoryColor.r:F2}, {original.StoryColor.g:F2}, {original.StoryColor.b:F2})";
                _cloneColorLabel.style.backgroundColor = original.StoryColor;
            }

            if (_cloneBuildLabel != null)
                _cloneBuildLabel.text = original.StoryBuildVersion.ToString();

            if (_memoToggle != null)
                _memoToggle.value = original.UseMemo;

            if (_memoField != null)
                _memoField.value = original.Memo ?? "";
        }
        else if (!string.IsNullOrEmpty(Data.OriginalStoryID))
        {
            
            _cloneWarningLabel.text = $"Original Story '{Data.OriginalStoryID}' not found!";
            _cloneWarningLabel.style.color = new Color(1f, 0.5f, 0.3f, 1f);
            title = string.Format(StoryCardUI.TitleFormat, Data.StoryName_KO, Data.StoryID);
        }
        else
        {
            
            _cloneWarningLabel.text = "Please specify Original Story ID";
            _cloneWarningLabel.style.color = new Color(1f, 0.8f, 0.3f, 1f);
        }
    }

    private void RefreshPlayStoryIdLabel()
    {
        if (_playStoryIdValue != null)
            _playStoryIdValue.text = Data.StoryIDToPlay;
    }

    private void ApplyStatusStyling()
    {
        
        float borderWidth = StoryCardUI.BorderWidth;
        Color borderColor = StoryCardUI.DefaultBaseColor;

        // Clone.
        if (Data.IsClone)
        {
            borderWidth = StoryCardUI.CloneBorderWidth;
            borderColor = StoryCardUI.CloneBorderColor;
        }

        // Dead end, which outranks the clone border.
        if (Data.IsDeadEnd)
        {
            borderWidth = StoryCardUI.DeadEndBorderWidth;
            borderColor = StoryCardUI.DeadEndBorderColor;
        }

        this.style.borderTopWidth    = borderWidth;
        this.style.borderRightWidth  = borderWidth;
        this.style.borderBottomWidth = borderWidth;
        this.style.borderLeftWidth   = borderWidth;
        this.style.borderTopColor    = borderColor;
        this.style.borderRightColor  = borderColor;
        this.style.borderBottomColor = borderColor;
        this.style.borderLeftColor   = borderColor;
    }

    public override void LoadFromData() =>
        SetPosition(new Rect(Data.NodePosition, StoryCardUI.CardSize));

    public override void SaveToData()
    {
        Data.NodePosition = GetPosition().position;
        EditorUtility.SetDirty(Data);
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);

        evt.menu.AppendAction("Auto Extract Thumbnail & Color", (action) =>
        {
            AutoExtractThumbnailAndColor();
        });

        evt.menu.AppendAction("Create Imagination Folder", (action) =>
        {
            CreateImaginationFolder();
        });
    }

    private void AutoExtractThumbnailAndColor()
    {
        if (Data == null)
        {
            Debug.LogError("[StoryCard] StoryData is null.");
            return;
        }

        bool success = StoryDataAutoExtractor.AutoExtract(Data, showLog: true);

        if (success)
        {
            
            RefreshThumbnailUI();
            Debug.Log($"[StoryCard] Extracted assets for '{Data.StoryIDToPlay}'.");
        }
    }

    private void RefreshThumbnailUI()
    {
        
        if (_thumbPreview != null && Data != null)
        {
            _thumbPreview.image = Data.StoryThumbnail ? Data.StoryThumbnail.texture : null;
            _thumbPreview.tintColor = Data.StoryColor;
        }

        if (_thumbField != null && !Data.IsClone)
        {
            _thumbField.value = Data.StoryThumbnail;
        }

        if (_colorField != null && !Data.IsClone)
        {
            _colorField.value = Data.StoryColor;
        }
    }

    private void CreateImaginationFolder()
    {
        if (Data == null || string.IsNullOrEmpty(Data.StoryIDToPlay))
        {
            Debug.LogError("[StoryCard] Cannot create an Imagination folder without a StoryID.");
            return;
        }

        string basePath = "Assets/Resources/Imaginations";
        string fullPath = $"{basePath}/{Data.StoryIDToPlay}";

        if (AssetDatabase.IsValidFolder(fullPath))
        {
            Debug.LogWarning($"[StoryCard] Imagination folder already exists: {fullPath}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath));
            return;
        }

        if (!AssetDatabase.IsValidFolder(basePath))
        {
            string[] pathParts = basePath.Split('/');
            string currentPath = pathParts[0];
            for (int i = 1; i < pathParts.Length; i++)
            {
                string nextPath = currentPath + "/" + pathParts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, pathParts[i]);
                }
                currentPath = nextPath;
            }
        }

        AssetDatabase.CreateFolder(basePath, Data.StoryIDToPlay);
        AssetDatabase.Refresh();
        Debug.Log($"[StoryCard] Created Imagination folder: {fullPath}");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fullPath));
    }

    /*──────────────────────── Memo Panel ───────────────*/
    private void CreateMemoPanel()
    {
        
        _memoPanel = new VisualElement
        {
            style =
            {
                position = Position.Absolute,
                left = 0,
                bottom = StoryCardUI.CardSize.y + 10,
                width = StoryCardUI.CardSize.x,
                minHeight = 120,
                maxHeight = 300,
                backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.95f),
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 10,
                paddingRight = 10,
                borderTopWidth = 2,
                borderBottomWidth = 2,
                borderLeftWidth = 2,
                borderRightWidth = 2,
                borderTopColor = new Color(0.4f, 0.6f, 0.8f, 1f),
                borderBottomColor = new Color(0.2f, 0.3f, 0.4f, 1f),
                borderLeftColor = new Color(0.3f, 0.4f, 0.6f, 1f),
                borderRightColor = new Color(0.3f, 0.4f, 0.6f, 1f),
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4,
                display = Data.UseMemo ? DisplayStyle.Flex : DisplayStyle.None
            }
        };

        var titleLabel = new Label("Memo")
        {
            style =
            {
                fontSize = 12,
                unityFontStyleAndWeight = FontStyle.Bold,
                color = new Color(0.8f, 0.9f, 1f, 1f),
                marginBottom = 4
            }
        };
        _memoPanel.Add(titleLabel);

        _memoField = new TextField()
        {
            value = Data.Memo ?? "",
            multiline = true,
            style =
            {
                flexGrow = 1,
                minHeight = 80
            }
        };

        _memoField.AddToClassList("unity-text-field");
        _memoField.AddToClassList("unity-text-field--multiline");

        _memoField.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            var textInput = _memoField.Q(className: "unity-text-field__input");
            if (textInput != null)
            {
                textInput.style.whiteSpace = WhiteSpace.Normal;
                textInput.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
                textInput.style.color = new Color(0.9f, 0.9f, 0.95f, 1f);
                textInput.style.paddingTop = 6;
                textInput.style.paddingBottom = 6;
                textInput.style.paddingLeft = 6;
                textInput.style.paddingRight = 6;
            }
        });

        _memoField.RegisterValueChangedCallback(e =>
        {
            Data.Memo = e.newValue;
            EditorUtility.SetDirty(Data);
        });

        _memoField.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
        _memoField.RegisterCallback<MouseUpEvent>(e => e.StopPropagation());

        _memoPanel.Add(_memoField);
        this.Add(_memoPanel);
    }

    private void UpdateMemoVisibility()
    {
        if (_memoPanel == null) return;
        _memoPanel.style.display = Data.UseMemo ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
#endif
