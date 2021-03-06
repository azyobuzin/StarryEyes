﻿using System;
using System.Collections.Generic;
using Livet;

namespace StarryEyes.Mystique.Models.Tab
{
    public static class TabManager
    {
        private static Stack<TabInfo> closedTabsStack = new Stack<TabInfo>();

        private static ObservableSynchronizedCollection<ObservableSynchronizedCollection<TabInfo>> tabs =
            new ObservableSynchronizedCollection<ObservableSynchronizedCollection<TabInfo>>();
        internal static ObservableSynchronizedCollection<ObservableSynchronizedCollection<TabInfo>> Tabs
        {
            get { return TabManager.tabs; }
        }

        /// <summary>
        /// Get column info datas for persistence.
        /// </summary>
        /// <returns></returns>
        internal static IEnumerable<ColumnInfo> GetColumnInfoData()
        {
            foreach (var tab in tabs)
            {
                yield return new ColumnInfo() { Tabs = new List<TabInfo>(tab) };
            }
        }

        private static int _currentFocusColumn = 0;
        /// <summary>
        /// Current focused column index
        /// </summary>
        public static int CurrentFocusColumn
        {
            get { return TabManager._currentFocusColumn; }
            set { TabManager._currentFocusColumn = value; }
        }

        /// <summary>
        /// Find tab info where existed.
        /// </summary>
        /// <param name="info">tab info</param>
        /// <param name="colIndex">column index</param>
        /// <param name="tabIndex">tab index</param>
        public static void GetTabInfoIndexes(TabInfo info, out int colIndex, out int tabIndex)
        {
            for (int ci = 0; ci < tabs.Count; ci++)
            {
                for (int ti = 0; ti < tabs[ci].Count; ti++)
                {
                    if (tabs[ci][ti] == info)
                    {
                        colIndex = ci;
                        tabIndex = ti;
                        return;
                    }
                }
            }
            throw new ArgumentException("specified TabInfo was not found.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="columnIndex"></param>
        /// <param name="tabIndex"></param>
        public static void MoveTo(TabInfo info, int columnIndex, int tabIndex)
        {
            int fci, fti;
            GetTabInfoIndexes(info, out fci, out fti);
            MoveTo(fci, fti, columnIndex, tabIndex);
        }

        /// <summary>
        /// Move specified tab.
        /// </summary>
        public static void MoveTo(int fromColumnIndex, int fromTabIndex, int destColumnIndex, int destTabIndex)
        {
            if (fromColumnIndex == destColumnIndex)
            {
                // in-column moving
                tabs[fromColumnIndex].Move(fromTabIndex, destTabIndex);
            }
            else
            {
                var tab = tabs[fromColumnIndex][fromTabIndex];
                tabs[fromColumnIndex].RemoveAt(fromTabIndex);
                tabs[destColumnIndex].Insert(destTabIndex, tab);
            }
        }

        /// <summary>
        /// Create tab
        /// </summary>
        /// <param name="info">tab information</param>
        public static void CreateTab(TabInfo info)
        {
            CreateTab(info, _currentFocusColumn);
        }

        /// <summary>
        /// Create tab into specified column
        /// </summary>
        public static void CreateTab(TabInfo info, int columnIndex)
        {
            if (columnIndex > tabs.Count) // column index is only for existed or new column
                throw new ArgumentOutOfRangeException("columnIndex", "currently " + tabs.Count + " columns are created. so, you can't set this parameter as " + columnIndex  + ".");
            if (columnIndex == tabs.Count)
            {
                // create new
                CreateColumn(info);
            }
            else
            {
                tabs[columnIndex].Add(info);
                info.FilterQuery.Activate();
            }
        }

        /// <summary>
        /// Create column
        /// </summary>
        /// <param name="info">initial created tab</param>
        public static void CreateColumn(TabInfo info)
        {
            tabs.Add(new ObservableSynchronizedCollection<TabInfo>(new[] { info }));
            info.FilterQuery.Activate();
        }

        /// <summary>
        /// Close a tab.
        /// </summary>
        public static void CloseTab(int colIndex, int tabIndex)
        {
            var ti = tabs[colIndex][tabIndex];
            ti.FilterQuery.Deactivate();
            closedTabsStack.Push(ti);
            tabs[colIndex].RemoveAt(tabIndex);
        }

        /// <summary>
        /// Check revivable tab is existed in closed tabs stack.
        /// </summary>
        public static bool IsRevivableTabExsted
        {
            get { return closedTabsStack.Count > 0; }
        }

        /// <summary>
        /// Revive tab from closed tabs stack.
        /// </summary>
        public static void ReviveTab()
        {
            var ti = closedTabsStack.Pop();
            ti.FilterQuery.Activate();
            CreateTab(ti);
        }

        /// <summary>
        /// Clear closed tabs stack.
        /// </summary>
        public static void CrearClosedTabsStack()
        {
            closedTabsStack.Clear();
        }
    }
}
