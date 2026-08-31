/*─────────────────────── StoryFlowEditor.cs ─────────────────────*/
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;

/// <summary>
/// Main window for the story flow tool: a GraphView canvas plus a filterable,
/// sortable side menu over every story, branch and sub-story asset in the project.
///
/// The graph is the authoring surface for the whole narrative. Cards are the
/// ScriptableObjects themselves, edges are the NextCardID fields, and the side
/// menu colours each entry by whether it is connected and whether its script
/// exists, so an orphaned or unwritten story is visible without opening it.
/// </summary>
public class StoryFlowEditor : EditorWindow
{
    
    private StoryFlowGraphView _graph;
    private ScrollView         _menu;
    private VisualElement      _menuContainer;
    private TextField          _searchField;
    
    // Type filters.
    bool _showStories = true;
    bool _showBranches = true;
    bool _showSubStories = true;

    private Label _storiesLabel;
    private Label _branchesLabel;
    private Label _subStoriesLabel;

    private DropdownField _sortDropdown;

    const string STORY_PATH    = "Stories/StoryDatas";
    const string BRANCH_PATH   = "Stories/BranchDatas";
    const string SUBSTORY_PATH = "Stories/SubStoryDatas";
    const string SCRIPT_FOLDER = "Assets/Resources/Stories/StoryScripts/";

    enum SortOption
    {
        Name,
        ID,
        CreationOrder,
        ScriptStatus    // Stories only: connected and scripted first.
    }

    [MenuItem("Greenwood/Story Branch &2")]
    public static void Open() =>
        GetWindow<StoryFlowEditor>("Story Flow Editor");

    void OnEnable()
    {
        
        _graph = new StoryFlowGraphView(STORY_PATH, BRANCH_PATH, SCRIPT_FOLDER);
        _graph.StretchToParentSize();

        _graph.SetupZoom(0.1f, 3.0f); // Far enough out to see a whole chapter at once.
        
        rootVisualElement.Add(_graph);

        _graph.RequestMenuRefresh = () => RefreshMenu(forceRebuild:true);
        _graph.RequestReload = Reload;

        var bar = new Toolbar();
        bar.Add(new Button(() => { _graph.SaveGraph(); RefreshMenu(forceRebuild:true); }) { text = "Save" });
        bar.Add(new Button(Reload) { text = "Reload" });
        bar.Add(new Button(() => _graph.SaveFullBackup()) { text = "Full Backup" });
        bar.Add(new Button(() => _graph.RestoreFromBackup()) { text = "Restore" });
        bar.Add(new Button(() => _graph.FrameAll()) { text = "Frame All" });
        bar.Add(new Button(AutoExtractAll) { text = "Auto Extract All" });
        rootVisualElement.Add(bar);

        _menuContainer = new VisualElement
        {
            style =
            {
                width = 340,
                height = Length.Percent(100),
                position = Position.Absolute,
                left = 12,
                top = 28,
                backgroundColor = new Color(0.15f, 0.15f, 0.18f, 0.95f),
                borderTopLeftRadius = 8,
                borderTopRightRadius = 8,
                borderBottomLeftRadius = 8,
                borderBottomRightRadius = 8,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftColor = new Color(0.35f, 0.35f, 0.4f, 0.8f),
                borderRightColor = new Color(0.35f, 0.35f, 0.4f, 0.8f),
                borderTopColor = new Color(0.35f, 0.35f, 0.4f, 0.8f),
                borderBottomColor = new Color(0.35f, 0.35f, 0.4f, 0.8f),
                paddingLeft = 8,
                paddingRight = 8,
                paddingTop = 12,
                paddingBottom = 8
            }
        };

        var header = new Label("Story Navigator")
        {
            style =
            {
                fontSize = 16,
                color = new Color(0.9f, 0.9f, 0.95f, 1f),
                unityFontStyleAndWeight = FontStyle.Bold,
                unityTextAlign = TextAnchor.MiddleCenter,
                marginBottom = 8,
                paddingBottom = 6,
                borderBottomWidth = 1,
                borderBottomColor = new Color(0.4f, 0.4f, 0.45f, 0.6f)
            }
        };
        _menuContainer.Add(header);

        CreateFilterSection();

        CreateSortSection();

        _searchField = new TextField()
        {
            style =
            {
                marginBottom = 8,
                height = 32,
                backgroundColor = new Color(0.2f, 0.2f, 0.25f, 1f),
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftColor = new Color(0.4f, 0.4f, 0.5f, 0.8f),
                borderRightColor = new Color(0.4f, 0.4f, 0.5f, 0.8f),
                borderTopColor = new Color(0.4f, 0.4f, 0.5f, 0.8f),
                borderBottomColor = new Color(0.4f, 0.4f, 0.5f, 0.8f)
            }
        };
        _searchField.RegisterValueChangedCallback(evt => RefreshMenu());
        _menuContainer.Add(_searchField);

        _menu = new ScrollView
        {
            style =
            {
                height = Length.Percent(100),
                marginTop = 4
            }
        };
        _menuContainer.Add(_menu);
        rootVisualElement.Add(_menuContainer);

        Reload();
        EnableArrowPan();
    }

    void CreateFilterSection()
    {
        var filterContainer = new VisualElement
        {
            style =
            {
                backgroundColor = new Color(0.18f, 0.18f, 0.22f, 0.8f),
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6,
                marginBottom = 8,
                paddingLeft = 8,
                paddingRight = 8,
                paddingTop = 10,
                paddingBottom = 15,
                minHeight = 70
            }
        };

        var filterLabel = new Label("Show Types")
        {
            style =
            {
                fontSize = 12,
                color = new Color(0.8f, 0.8f, 0.85f, 1f),
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 8
            }
        };
        filterContainer.Add(filterLabel);

        var filterRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceAround,
                height = 25,
                alignItems = Align.Center
            }
        };

        _storiesLabel = new Label("Stories")
        {
            style =
            {
                fontSize = 11,
                color = new Color(0.3f, 0.8f, 0.4f, 1f), 
                unityFontStyleAndWeight = FontStyle.Bold,
                width = 80,
                height = 25,
                unityTextAlign = TextAnchor.MiddleCenter,
                backgroundColor = new Color(0.2f, 0.2f, 0.25f, 0.6f),
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4,
                paddingTop = 4
            }
        };
        _storiesLabel.RegisterCallback<MouseDownEvent>(e => ToggleStoriesFilter());

        _branchesLabel = new Label("Branches")
        {
            style =
            {
                fontSize = 11,
                color = new Color(0.8f, 0.5f, 0.3f, 1f), 
                unityFontStyleAndWeight = FontStyle.Bold,
                width = 85,
                height = 25,
                unityTextAlign = TextAnchor.MiddleCenter,
                backgroundColor = new Color(0.2f, 0.2f, 0.25f, 0.6f),
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4,
                paddingTop = 4
            }
        };
        _branchesLabel.RegisterCallback<MouseDownEvent>(e => ToggleBranchesFilter());

        _subStoriesLabel = new Label("SubStories")
        {
            style =
            {
                fontSize = 11,
                color = new Color(0.7f, 0.4f, 0.9f, 1f), 
                unityFontStyleAndWeight = FontStyle.Bold,
                width = 95,
                height = 25,
                unityTextAlign = TextAnchor.MiddleCenter,
                backgroundColor = new Color(0.2f, 0.2f, 0.25f, 0.6f),
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4,
                paddingTop = 4
            }
        };
        _subStoriesLabel.RegisterCallback<MouseDownEvent>(e => ToggleSubStoriesFilter());

        AddHoverEffect(_storiesLabel);
        AddHoverEffect(_branchesLabel);
        AddHoverEffect(_subStoriesLabel);

        var firstRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceAround,
                height = 25,
                alignItems = Align.Center,
                marginBottom = 4
            }
        };
        firstRow.Add(_storiesLabel);
        firstRow.Add(_branchesLabel);

        var secondRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceAround,
                height = 25,
                alignItems = Align.Center
            }
        };
        secondRow.Add(_subStoriesLabel);

        filterContainer.Add(firstRow);
        filterContainer.Add(secondRow);
        _menuContainer.Add(filterContainer);
    }

    void AddHoverEffect(Label label)
    {
        var originalBg = label.style.backgroundColor;
        
        label.RegisterCallback<MouseEnterEvent>(e =>
        {
            label.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            label.style.scale = new Scale(Vector3.one * 1.05f);
        });

        label.RegisterCallback<MouseLeaveEvent>(e =>
        {
            label.style.backgroundColor = originalBg;
            label.style.scale = new Scale(Vector3.one);
        });
    }

    void ToggleStoriesFilter()
    {
        _showStories = !_showStories;
        _storiesLabel.style.color = _showStories 
            ? new Color(0.3f, 0.8f, 0.4f, 1f)      // on
            : new Color(0.5f, 0.5f, 0.55f, 0.7f);  // off
        _storiesLabel.style.unityFontStyleAndWeight = _showStories ? FontStyle.Bold : FontStyle.Normal;
        RefreshMenu();
    }

    void ToggleBranchesFilter()
    {
        _showBranches = !_showBranches;
        _branchesLabel.style.color = _showBranches
            ? new Color(0.8f, 0.5f, 0.3f, 1f)      // on
            : new Color(0.5f, 0.5f, 0.55f, 0.7f);  // off
        _branchesLabel.style.unityFontStyleAndWeight = _showBranches ? FontStyle.Bold : FontStyle.Normal;
        RefreshMenu();
    }

    void ToggleSubStoriesFilter()
    {
        _showSubStories = !_showSubStories;
        _subStoriesLabel.style.color = _showSubStories
            ? new Color(0.7f, 0.4f, 0.9f, 1f)      // on
            : new Color(0.5f, 0.5f, 0.55f, 0.7f);  // off
        _subStoriesLabel.style.unityFontStyleAndWeight = _showSubStories ? FontStyle.Bold : FontStyle.Normal;
        RefreshMenu();
    }

    void CreateSortSection()
    {
        var sortContainer = new VisualElement
        {
            style =
            {
                backgroundColor = new Color(0.18f, 0.18f, 0.22f, 0.8f),
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6,
                marginBottom = 8,
                paddingLeft = 8,
                paddingRight = 8,
                paddingTop = 10,
                paddingBottom = 15,
                minHeight = 70
            }
        };

        var sortLabel = new Label("Sort By")
        {
            style =
            {
                fontSize = 12,
                color = new Color(0.8f, 0.8f, 0.85f, 1f),
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 10
            }
        };
        sortContainer.Add(sortLabel);

        var sortOptions = new List<string> 
        { 
            "Name (A-Z)", 
            "ID (A-Z)", 
            "Creation Order", 
            "Script Status" 
        };

        _sortDropdown = new DropdownField()
        {
            choices = sortOptions,
            index = 0,
            style =
            {
                height = 30,
                backgroundColor = new Color(0.2f, 0.2f, 0.25f, 1f),
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4,
                color = new Color(0.9f, 0.9f, 0.95f, 1f),
                fontSize = 11
            }
        };
        _sortDropdown.RegisterValueChangedCallback(evt => RefreshMenu());

        sortContainer.Add(_sortDropdown);
        _menuContainer.Add(sortContainer);
    }

    bool HasScriptFile(string storyID)
    {
        string scriptPath = Path.Combine(SCRIPT_FOLDER, $"{storyID}.cs");
        return File.Exists(scriptPath);
    }

    bool IsConnected(StoryCard card)
    {
        if (card == null) return false;

        bool hasInputConnection = card.InputPort != null && card.InputPort.connections.Any();
        bool hasOutputConnection = card.OutputPort != null && card.OutputPort.connections.Any();

        return hasInputConnection || hasOutputConnection;
    }

    void Reload()
    {
        // Forced, because Resources caches the previous load and the graph would
        // otherwise rebuild from stale assets after an external edit.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        EditorUtility.UnloadUnusedAssetsImmediate(true);
        Debug.Log("[StoryFlowEditor] Asset database refreshed.");

        var stories  = Resources.LoadAll<StoryData>(STORY_PATH).ToList();
        var branches = Resources.LoadAll<BranchData>(BRANCH_PATH).ToList();
        var subStories = Resources.LoadAll<SubStoryData>(SUBSTORY_PATH).ToList();

        Debug.Log($"[StoryFlowEditor] LoadGraph: Story={stories.Count}, Branch={branches.Count}, SubStory={subStories.Count}");
        _graph.LoadGraph(stories, branches, subStories);
        RefreshMenu(forceRebuild:true);
    }

    IOrderedEnumerable<StoryCard> SortStories(IEnumerable<StoryCard> stories, SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.Name => stories.OrderBy(s => s.Data.DisplayName),
            SortOption.ID => stories.OrderBy(s => s.Data.StoryID),
            SortOption.CreationOrder => stories.OrderBy(s => s.Data.name),
            SortOption.ScriptStatus => stories
                .OrderByDescending(s => IsConnected(s))
                .ThenByDescending(s => HasScriptFile(s.Data.StoryID))
                .ThenBy(s => s.Data.DisplayName),
            _ => stories.OrderBy(s => s.Data.DisplayName)
        };
    }

    IOrderedEnumerable<BranchCard> SortBranches(IEnumerable<BranchCard> branches, SortOption sortOption)
    {
        return sortOption switch
        {
            SortOption.Name => branches.OrderBy(b => b.Data.name),
            SortOption.ID => branches.OrderBy(b => b.Data.name), // A branch has no id of its own; the asset name is it.
            SortOption.CreationOrder => branches.OrderBy(b => b.Data.name),
            SortOption.ScriptStatus => branches.OrderBy(b => b.Data.name),
            _ => branches.OrderBy(b => b.Data.name)
        };
    }

    void RefreshMenu(bool forceRebuild = false)
    {
        if (forceRebuild)
        {
            _graph.DisplayOrder.Clear();
            _graph.DisplayOrder.AddRange(_graph.graphElements.Where(n => n is StoryCard || n is BranchCard || n is SubStoryCard));
        }
        else if (_graph.DisplayOrder.Count == 0)
        {
            _graph.DisplayOrder.AddRange(_graph.graphElements.Where(n => n is StoryCard || n is BranchCard || n is SubStoryCard));
        }

        _menu.Clear();

        string searchText = _searchField?.value?.ToLower() ?? "";
        bool hasSearch = !string.IsNullOrWhiteSpace(searchText);

        bool showStories = _showStories;
        bool showBranches = _showBranches;
        bool showSubStories = _showSubStories;

        var sortOption = (SortOption)(_sortDropdown?.index ?? 0);

        var allStories = _graph.DisplayOrder.OfType<StoryCard>().Where(sc => !sc.Data.IsClone).ToList();
        var allClones = _graph.DisplayOrder.OfType<StoryCard>().Where(sc => sc.Data.IsClone).ToList();
        var allBranches = _graph.DisplayOrder.OfType<BranchCard>().ToList();
        var allSubStories = _graph.DisplayOrder.OfType<SubStoryCard>().ToList();

        int totalCount = 0;

        if (showStories && allStories.Any())
        {
            var filteredStories = hasSearch 
                ? allStories.Where(s => s.Data.DisplayName.ToLower().Contains(searchText) || 
                                       s.Data.StoryID.ToLower().Contains(searchText)).ToList()
                : allStories;

            if (filteredStories.Any())
            {
                var sortedStories = SortStories(filteredStories, sortOption);

                AddSectionHeader($"Stories ({filteredStories.Count})");
                foreach (var sc in sortedStories)
                {
                    bool hasScript = HasScriptFile(sc.Data.StoryID);
                    bool isConnected = IsConnected(sc);

                    string disconnectedSuffix = isConnected ? "" : " (disconnected)";

                    // Colour encodes both axes at once: green is connected and written,
                    // red is neither, so unfinished work is visible in the list itself.
                    Color accentColor;
                    if (isConnected && hasScript)
                        accentColor = new Color(0.3f, 0.8f, 0.4f, 0.8f);  
                    else if (isConnected && !hasScript)
                        accentColor = new Color(0.9f, 0.8f, 0.3f, 0.8f);  
                    else if (!isConnected && hasScript)
                        accentColor = new Color(0.9f, 0.6f, 0.3f, 0.8f);  
                    else
                        accentColor = new Color(0.8f, 0.4f, 0.3f, 0.8f);  

                    string scriptStatus = hasScript ? "Ready" : "Missing";

                    CreateMenuButton(sc,
                        $"{sc.Data.DisplayName}{disconnectedSuffix}",
                        $"ID: {sc.Data.StoryID} • Script: {scriptStatus}",
                        accentColor,
                        true); // Stories move the camera; other card types only highlight.
                    totalCount++;
                }
            }
        }

        if (showStories && allClones.Any())
        {
            var filteredClones = hasSearch
                ? allClones.Where(c => c.Data.DisplayName.ToLower().Contains(searchText) ||
                                      (!string.IsNullOrEmpty(c.Data.OriginalStoryID) && c.Data.OriginalStoryID.ToLower().Contains(searchText))).ToList()
                : allClones;

            if (filteredClones.Any())
            {
                if (totalCount > 0) AddSpacer();
                AddSectionHeader($"Clones ({filteredClones.Count})");
                foreach (var cc in filteredClones.OrderBy(c => c.Data.DisplayName))
                {
                    CreateMenuButton(cc, $"{cc.Data.DisplayName}",
                        !string.IsNullOrEmpty(cc.Data.OriginalStoryID) ? $"Clone of: {cc.Data.OriginalStoryID}" : "No Reference",
                        new Color(0.3f, 0.6f, 1f, 0.6f), true);
                    totalCount++;
                }
            }
        }

        if (showBranches && allBranches.Any())
        {
            var filteredBranches = hasSearch
                ? allBranches.Where(b => b.Data.name.ToLower().Contains(searchText)).ToList()
                : allBranches;

            if (filteredBranches.Any())
            {
                var sortedBranches = SortBranches(filteredBranches, sortOption);

                if (totalCount > 0) AddSpacer();
                AddSectionHeader($"Branches ({filteredBranches.Count})");
                foreach (var bc in sortedBranches)
                {
                    CreateMenuButton(bc, $"{bc.Data.name}", "Branch Node",
                        new Color(0.8f, 0.5f, 0.3f, 0.6f), true);
                    totalCount++;
                }
            }
        }

        if (showSubStories && allSubStories.Any())
        {
            var filteredSubStories = hasSearch
                ? allSubStories.Where(ss => (ss.Data.cardID ?? "").ToLower().Contains(searchText) ||
                                           (ss.Data.candidateStoryIds != null && ss.Data.candidateStoryIds.Any(id => id.ToLower().Contains(searchText)))).ToList()
                : allSubStories;

            if (filteredSubStories.Any())
            {
                if (totalCount > 0) AddSpacer();
                AddSectionHeader($"SubStories ({filteredSubStories.Count})");
                foreach (var ssc in filteredSubStories.OrderBy(ss => ss.Data.cardID))
                {
                    string candidatesInfo = ssc.Data.candidateStoryIds != null && ssc.Data.candidateStoryIds.Any()
                        ? $"{ssc.Data.candidateStoryIds.Count} candidates"
                        : "No candidates";
                    CreateMenuButton(ssc, $"{ssc.Data.cardID}", candidatesInfo,
                        new Color(0.7f, 0.4f, 0.9f, 0.6f), true);
                    totalCount++;
                }
            }
        }

        if (hasSearch && totalCount == 0)
        {
            var noResult = new Label("No results found")
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new Color(0.6f, 0.6f, 0.6f, 1f),
                    fontSize = 14,
                    marginTop = 20,
                    marginBottom = 20
                }
            };
            _menu.Add(noResult);
        }

        else if (!showStories && !showBranches && !showSubStories)
        {
            var noFilter = new Label("All types are hidden\nEnable at least one type filter")
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new Color(0.8f, 0.6f, 0.4f, 1f),
                    fontSize = 12,
                    marginTop = 20,
                    marginBottom = 20,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            _menu.Add(noFilter);
        }
    }

    void AddSectionHeader(string title)
    {
        var header = new Label(title)
        {
            style =
            {
                fontSize = 12,
                color = new Color(0.8f, 0.8f, 0.85f, 1f),
                unityFontStyleAndWeight = FontStyle.Bold,
                unityTextAlign = TextAnchor.MiddleLeft,
                marginTop = 8,
                marginBottom = 6,
                paddingLeft = 4,
                paddingBottom = 3,
                borderBottomWidth = 1,
                borderBottomColor = new Color(0.4f, 0.4f, 0.45f, 0.4f)
            }
        };
        _menu.Add(header);
    }

    void AddSpacer()
    {
        var spacer = new VisualElement
        {
            style = { height = 12 }
        };
        _menu.Add(spacer);
    }

    void CreateMenuButton(GraphElement element, string title, string subtitle, Color accentColor, bool enableFocus = false)
    {
        var container = new VisualElement
        {
            style =
            {
                backgroundColor = new Color(0.22f, 0.22f, 0.26f, 0.8f),
                borderTopLeftRadius = 8,
                borderTopRightRadius = 8,
                borderBottomLeftRadius = 8,
                borderBottomRightRadius = 8,
                marginBottom = 6,
                marginLeft = 2,
                marginRight = 2,
                paddingLeft = 14,
                paddingRight = 14,
                paddingTop = 10,
                paddingBottom = 10,
                borderLeftWidth = 4,
                borderLeftColor = accentColor,
                minHeight = 50
            }
        };

        var titleLabel = new Label(title)
        {
            style =
            {
                fontSize = 14,
                color = new Color(0.9f, 0.9f, 0.95f, 1f),
                unityFontStyleAndWeight = FontStyle.Bold,
                whiteSpace = WhiteSpace.Normal,
                marginBottom = 3
            }
        };

        var subtitleLabel = new Label(subtitle)
        {
            style =
            {
                fontSize = 11,
                color = new Color(0.7f, 0.7f, 0.75f, 1f),
                whiteSpace = WhiteSpace.Normal
            }
        };

        container.Add(titleLabel);
        container.Add(subtitleLabel);

        container.RegisterCallback<MouseEnterEvent>(e =>
        {
            container.style.backgroundColor = new Color(0.28f, 0.28f, 0.32f, 0.95f);
            container.style.borderLeftColor = new Color(accentColor.r, accentColor.g, accentColor.b, 1f);
            container.style.scale = new Scale(Vector3.one * 1.02f);
        });

        container.RegisterCallback<MouseLeaveEvent>(e =>
        {
            container.style.backgroundColor = new Color(0.22f, 0.22f, 0.26f, 0.8f);
            container.style.borderLeftColor = accentColor;
            container.style.scale = new Scale(Vector3.one);
        });

        container.RegisterCallback<MouseDownEvent>(e =>
        {
            _graph.ClearSelection();
            element.Select(_graph, true);
            
            if (enableFocus)
            {
                
                var elementRect = element.GetPosition();
                var center = elementRect.center;
                
                // Slightly zoomed out, so the node's neighbours are visible too.
                _graph.viewTransform.scale = Vector3.one * 0.8f;

                var graphRect = _graph.contentRect;
                var targetPos = new Vector3(
                    -center.x * 0.8f + graphRect.width * 0.5f,
                    -center.y * 0.8f + graphRect.height * 0.5f,
                    0
                );
                _graph.viewTransform.position = targetPos;
            }
            else
            {
                _graph.FrameSelection();
            }
        });

        _menu.Add(container);
    }

    void ResetGraphView()
    {
        _graph.viewTransform.position = Vector3.zero;
        _graph.viewTransform.scale = Vector3.one;
    }

    void EnableArrowPan()
    {
        _graph.focusable = true;
        _graph.RegisterCallback<KeyDownEvent>(ev =>
        {
            float s = 60f;
            var p   = _graph.viewTransform.position;
            switch (ev.keyCode)
            {
                case KeyCode.UpArrow:    p.y += s; break;
                case KeyCode.DownArrow:  p.y -= s; break;
                case KeyCode.LeftArrow:  p.x += s; break;
                case KeyCode.RightArrow: p.x -= s; break;
                case KeyCode.Home:       
                    ResetGraphView();
                    return;
            }
            _graph.viewTransform.position = p;
        });
        _graph.Focus();
    }

    /* Batch thumbnail and colour extraction across every story. */
    void AutoExtractAll()
    {
        if (EditorUtility.DisplayDialog("Auto Extract All",
            "Extract thumbnails and colours for every story?\nOnly stories that have a script are processed.",
            "Yes", "Cancel"))
        {
            var allStories = Resources.LoadAll<StoryData>(STORY_PATH);
            int successCount = 0;
            int failCount = 0;

            EditorUtility.DisplayProgressBar("Auto Extract", "Scanning stories...", 0f);

            try
            {
                for (int i = 0; i < allStories.Length; i++)
                {
                    var story = allStories[i];
                    float progress = (float)i / allStories.Length;
                    EditorUtility.DisplayProgressBar("Auto Extract",
                        $"Processing {story.StoryIDToPlay} ({i + 1}/{allStories.Length})",
                        progress);

                    bool success = StoryDataAutoExtractor.AutoExtract(story, showLog: false);
                    if (success)
                        successCount++;
                    else
                        failCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[AutoExtractAll] Done. Succeeded: {successCount}, failed or skipped: {failCount}");
            EditorUtility.DisplayDialog("Auto Extract Complete",
                $"Extraction complete.\n\nSucceeded: {successCount}\nFailed or skipped: {failCount}",
                "OK");

            Reload();
        }
    }

    void OnDisable()
    {
        rootVisualElement.Clear();
    }
}
#endif
