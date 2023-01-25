using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Search;
using UnityEngine.UIElements;
using UnityEditor.Search;
using System;
using UnityEditor;
using System.Linq;
using System.IO;

class NavigationToolbar : SearchElement
{
    Button m_BackHistoryButton;
    Button m_ForwardHistoryButton;
    PopupField<string> m_NavStack;
    List<string> m_NavStackValues;
    List<Action> m_SearchEventOffs;
    ISearchQuery m_CurrentQuery;
    NavigableStack<ISearchQuery> m_QueryHistory;

    const string ussClassName = "search-nav-bar";

    public NavigationToolbar(ISearchView viewModel)
        : base(nameof(NavigationToolbar), viewModel, ussClassName, "search-toolbar")
    {
        AddStyleSheetPath(ProjectExplorerWindow.kProjectExplorerStyleSheet);

        m_BackHistoryButton = new Button(OnBack);
        m_BackHistoryButton.text = "<";
        m_BackHistoryButton.AddToClassList("search-toolbar__button");
        Add(m_BackHistoryButton);

        m_ForwardHistoryButton = new Button(OnForward);
        m_ForwardHistoryButton.text = ">";
        m_ForwardHistoryButton.AddToClassList("search-toolbar__button");
        Add(m_ForwardHistoryButton);

        var separator = new VisualElement();
        separator.AddToClassList("search-toolbar__separator");
        Add(separator);

        m_NavStackValues = new List<string>(new[] { "boo", "bing", "bong" });
        m_NavStack = new PopupField<string>("", m_NavStackValues, 0);
        m_NavStack.AddToClassList("search-toolbar__popup");
        m_NavStack.RegisterCallback<ChangeEvent<string>>(OnNavStackChanged);
        Add(m_NavStack);

        m_QueryHistory = new NavigableStack<ISearchQuery>(20);

        m_SearchEventOffs = new List<Action>()
        {
            On(SearchEvent.SearchQueryExecuted, HandleQueryExecuted),
        };

        OnHistoryChanged();
    }

    protected override void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        base.OnDetachFromPanel(evt);
        m_SearchEventOffs.ForEach(off => off());
    }

    void OnNavStackChanged(ChangeEvent<string> evt)
    {
        var tokens = new List<string>();
        for(var i= 0; i <= m_NavStack.index; ++i)
        {
            tokens.Add(m_NavStack.choices[i]);
        }
        var path = string.Join("/", tokens);
        var query = FileSystemNodeHandler.CreateListFolderQuery(path);
        Emit(SearchEvent.ExecuteSearchQuery, query);
    }

    void HandleQueryExecuted(ISearchEvent evt)
    {
        var query = evt.GetArgument<ISearchQuery>(0);
        if (m_CurrentQuery != query)
        {
            m_CurrentQuery = query;
            PushQuery(query);
            OnHistoryChanged();
        }
    }

    void OnBack()
    {
        if (m_QueryHistory.CanNavigateBackward() && m_QueryHistory.NavigateBackward(out var query))
        {
            m_CurrentQuery = query;
            Emit(SearchEvent.ExecuteSearchQuery, query);
        }

        OnHistoryChanged();
    }

    void OnForward()
    {
        if (m_QueryHistory.CanNavigateForward() && m_QueryHistory.NavigateForward(out var query))
        {
            m_CurrentQuery = query;
            Emit(SearchEvent.ExecuteSearchQuery, query);
        }
        OnHistoryChanged();
    }

    void PushQuery(ISearchQuery query)
    {
        m_CurrentQuery = query;
        m_QueryHistory.Push(query);
    }

    void OnHistoryChanged()
    {
        m_BackHistoryButton.SetEnabled(m_QueryHistory.CanNavigateBackward());
        m_ForwardHistoryButton.SetEnabled(m_QueryHistory.CanNavigateForward());

        if (m_CurrentQuery != null)
            UpdateFolderNavigationStack(m_CurrentQuery);
    }

    void UpdateFolderNavigationStack(ISearchQuery query)
    {
        if (FileSystemNodeHandler.TryGetFolderQuery(query.searchText, out var folder))
        {
            var pathTokens = folder.Split("/").ToList();
            m_NavStack.choices = pathTokens;
            m_NavStack.SetValueWithoutNotify(m_NavStack.choices[pathTokens.Count - 1]);
        }
        else
        {
            m_NavStack.choices = new List<string>() { query.displayName };
            m_NavStack.SetValueWithoutNotify(m_NavStack.choices[0]);
        }
    }

    void UpdateHistoryStack()
    {
        // TODO: this is a test to allow easy debugging of the HistoryStack
        if (m_QueryHistory.count == 0)
            return;
        var choices = m_QueryHistory.Select(q => q.displayName).ToList();
        m_NavStack.choices = choices;
        m_NavStack.SetValueWithoutNotify(m_NavStack.choices[0]);
    }
}
