using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static TarkovPriceViewer.Models.TarkovAPI;
using Item = TarkovPriceViewer.Models.TarkovAPI.Item;
using TarkovPriceViewer.Models;
using TarkovPriceViewer.Services;

namespace TarkovPriceViewer.UI
{
    public partial class Overlay : Form
    {
        private ISettingsService _settingsService;
        private ITarkovDataService _tarkovDataService;
        private ITarkovTrackerService _tarkovTrackerService;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private static readonly int GWL_EXSTYLE = -20;
        private static readonly int WS_EX_TOOLWINDOW = 0x00000080;
        private static readonly int WS_EX_LAYERED = 0x80000;
        private static readonly int WS_EX_TRANSPARENT = 0x20;
        private static int compare_size = 0;
        private static bool isinfoform = true;
        private static bool ismoving = false;
        private static int x, y;
        public static DateTime startTime;
        private static string waitinfForTooltipText = MainForm.languageModel == null ? Program.languageLoading : Program.waitingForTooltip;
        private static int DotsCounter = 0;

        private Object _lock = new Object();

        private Item _currentItem;

        private static readonly Dictionary<string, string> LootTierByName = Models.LootTierMapping.ByName;

        public void InitializeServices(
            ISettingsService settingsService,
            ITarkovDataService tarkovDataService,
            ITarkovTrackerService tarkovTrackerService)
        {
            _settingsService = settingsService;
            _tarkovDataService = tarkovDataService;
            _tarkovTrackerService = tarkovTrackerService;
        }

        public Overlay(bool _isinfoform)
        {
            InitializeComponent();
            isinfoform = _isinfoform;
            this.TopMost = true;
            var style = GetWindowLong(this.Handle, GWL_EXSTYLE);
            if (isinfoform)
            {
                // If services are not initialized yet, fall back to default opacity 0.8
                var opacity = _settingsService?.Settings.OverlayTransparent ?? 80;
                this.Opacity = opacity * 0.01;
                SetWindowLong(this.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
            }
            else
            {
                SetWindowLong(this.Handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
            }
            settingFormPos();
            initializeCompareData();
            initializeBallistics();
            iteminfo_panel.Visible = false;
            itemcompare_panel.Visible = false;
        }

        public void settingFormPos()
        {
            this.Location = new Point(0, 0);
            this.Size = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        }

        public void initializeCompareData()
        {
            ItemCompareGrid.ColumnCount = 7;
            ItemCompareGrid.Columns[0].Name = "Name";
            ItemCompareGrid.Columns[1].Name = "Recoil";
            ItemCompareGrid.Columns[2].Name = "Accuracy";
            ItemCompareGrid.Columns[3].Name = "Ergo";
            ItemCompareGrid.Columns[4].Name = "Flea";
            ItemCompareGrid.Columns[5].Name = "NPC";
            ItemCompareGrid.Columns[6].Name = "LL";
            ItemCompareGrid.Visible = false;
            ItemCompareGrid.ClearSelection();
            ItemCompareGrid.SortCompare += new DataGridViewSortCompareEventHandler(ItemCompareGrid_SortCompare);
            ResizeGrid(ItemCompareGrid);
        }

        public void initializeBallistics()
        {
            iteminfo_ball.ColumnCount = 9;
            iteminfo_ball.Columns[0].Name = "Type";
            iteminfo_ball.Columns[1].Name = "Name";
            iteminfo_ball.Columns[2].Name = "Damage";
            iteminfo_ball.Columns[3].Name = "1";
            iteminfo_ball.Columns[4].Name = "2";
            iteminfo_ball.Columns[5].Name = "3";
            iteminfo_ball.Columns[6].Name = "4";
            iteminfo_ball.Columns[7].Name = "5";
            iteminfo_ball.Columns[8].Name = "6";
            iteminfo_ball.Visible = false;
            iteminfo_ball.ClearSelection();
            ResizeGrid(iteminfo_ball);
        }

        private void ItemCompareGrid_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Index == 0 || e.Column.Index == 5 || e.Column.Index == 6) return;
            int s1 = 0;
            Int32.TryParse(String.Join("", e.CellValue1.ToString().Replace(",", "").Split(Program.splitcur)), out s1);
            int s2 = 0;
            Int32.TryParse(String.Join("", e.CellValue2.ToString().Replace(",", "").Split(Program.splitcur)), out s2);
            e.SortResult = s1.CompareTo(s2);
            e.Handled = true;
        }

        public void ResizeGrid(DataGridView view)
        {
            view.ClientSize = new Size(view.Columns.GetColumnsWidth(DataGridViewElementStates.None) + 10,
                view.Rows.GetRowsHeight(DataGridViewElementStates.None) + 22);
            view.Refresh();
        }

        public void SetBallisticsColorAPI(Item item)
        {
            for (int b = 0; b < iteminfo_ball.Rows.Count; b++)
            {
                for (int i = 0; i < iteminfo_ball.Rows[b].Cells.Count; i++)
                {
                    if (i == 1)
                    {
                        if (iteminfo_ball.Rows[b].Cells[i].Value.Equals(item.name))
                        {
                            iteminfo_ball.Rows[b].Cells[i].Style.ForeColor = Color.Gold;
                            iteminfo_ball.Rows[b].Cells[i - 1].Style.ForeColor = Color.Gold;
                            iteminfo_ball.Rows[b].Cells[i + 1].Style.ForeColor = Color.Gold;
                        }
                    }
                    else if (i >= 3)
                    {
                        try
                        {
                            int level = 0;
                            Int32.TryParse((String)iteminfo_ball.Rows[b].Cells[i].Value, out level);
                            iteminfo_ball.Rows[b].Cells[i].Style.BackColor = Program.BEColor[level];
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                        }
                    }
                }
            }
            iteminfo_ball.Refresh();
        }

        public void ShowInfoAPI(Item item, CancellationToken cts_one)
        {
            Action show = delegate ()
            {
                if (!cts_one.IsCancellationRequested)
                {
                    lock (_lock)
                    {
                        if (item == null || item.name == null)
                        {
                            if (MainForm.timer.Enabled || MainForm.WaitingForTooltip)
                                iteminfo_text.Text = waitinfForTooltipText;
                            else
                                iteminfo_text.Text = Program.notfound;
                        }
                        else if (item.link == null)
                        {
                            iteminfo_text.Text = Program.noflea;
                        }
                        else
                        {
                            _currentItem = item;
                            Debug.WriteLine("Market Link: " + item.link);

                            string mainCurrency = Program.rouble.ToString();
                            string BestSellTo_vendorName = "";
                            string BestBuyFrom_vendorName = "";

                            StringBuilder sb = new StringBuilder();

                            // Loot tier
                            SetLootTierPerSlot(item);
                            if (item.lootTier != null)
                                sb.Append(String.Format("{0}", item.lootTier));

                            // Name
                            sb.Append(String.Format("\n{0}", item.name));

                            // Helmet/armour class
                            if (item.properties != null && item.properties._class != null)
                            {
                                item.className = "Class " + item.properties._class.Value;
                                if (item.properties._class.Value > 0)
                                    sb.Append(String.Format("\n{0}\n", item.className));
                            }

                            // Key info
                            if (item.types.Any(e => e.Equals("keys")) && item.wikiLink != null)
                            {
                                string lockLocation = FindKeyInfo(item);
                                if (lockLocation != null)
                                {
                                    sb = RemoveTrailingLineBreaks(sb);
                                    sb.Append(String.Format("\n\nUse Location: \n{0}", lockLocation));
                                }
                            }

                            // Find Flea Market profit
                            int flea_profit = 0;
                            if (item.lastLowPrice != null && item.fleaMarketFee != null)
                            {
                                flea_profit = item.lastLowPrice.Value - item.fleaMarketFee.Value;
                            }

                            // Find best trader to sell to
                            if (item.sellFor.Count > 0)
                            {
                                List<SellFor> list = new List<SellFor>(item.sellFor);
                                List<SellFor> sortedVendor = new List<SellFor>(list.OrderByDescending(p => p.priceRUB));
                                var lastSortedVendor = item.sellFor[0];

                                if (sortedVendor[0].vendor.name == "Flea Market" && sortedVendor.Count > 1 && flea_profit > 0)
                                {
                                    if (flea_profit > sortedVendor[1].priceRUB.Value)
                                    {
                                        lastSortedVendor = sortedVendor[0];
                                    }
                                    else
                                    {
                                        lastSortedVendor = sortedVendor[1];
                                    }
                                }

                                BestSellTo_vendorName = lastSortedVendor.vendor.name;

                                int vendorPrice = lastSortedVendor.priceRUB.Value;
                                if (lastSortedVendor.vendor.name == "Flea Market" && item.lastLowPrice != null)
                                    vendorPrice = flea_profit;

                                if (lastSortedVendor.vendor.minTraderLevel != null)
                                    BestSellTo_vendorName += " LL" + lastSortedVendor.vendor.minTraderLevel;

                                if (vendorPrice > 0)
                                {
                                    string pricePerSlotDetails = GetPricePerSlotDetails(item, vendorPrice, mainCurrency);
                                    sb = RemoveTrailingLineBreaks(sb);
                                    sb.Append(String.Format("\n\nBest sell to {0} --> {1}{2}{3}", BestSellTo_vendorName, vendorPrice.ToString("N0"), mainCurrency, pricePerSlotDetails));
                                }
                            }

                            // Find best trader to buy from
                            if (item.buyFor.Count > 0)
                            {
                                List<BuyFor> list = new List<BuyFor>(item.buyFor);
                                var sortedVendor = list.OrderBy(p => p.priceRUB).First();
                                BestBuyFrom_vendorName = sortedVendor.vendor.name;

                                int vendorPrice = sortedVendor.priceRUB.Value;
                                if (sortedVendor.vendor.name == "Flea Market" && item.lastLowPrice != null)
                                    vendorPrice = item.lastLowPrice.Value;

                                if (sortedVendor.vendor.minTraderLevel != null)
                                    BestBuyFrom_vendorName += " LL" + sortedVendor.vendor.minTraderLevel;

                                if (vendorPrice > 0)
                                    sb.Append(String.Format("\nBest buy from {0} --> {1}{2}", BestBuyFrom_vendorName, vendorPrice.ToString("N0"), mainCurrency));
                            }

                            if (_settingsService?.Settings.ShowLastPrice == true && item.lastLowPrice != null)
                            {
                                sb = RemoveTrailingLineBreaks(sb);
                                var lastupdate = item.updated == null ? "" : Program.LastUpdated((DateTime)item.updated);
                                sb.Append(String.Format("\n\nLast Price : {0}{1}  ({2})", ((int)item.lastLowPrice).ToString("N0"), mainCurrency, lastupdate));
                            }
                            if (item.fleaMarketFee != null && !item.types.Exists(e => e.Equals("preset")))
                            {
                                if (flea_profit > 0)
                                    sb.Append(String.Format("\nProfit : {0}{1} (Fee : {2}{3})", flea_profit.ToString("N0"), mainCurrency, item.fleaMarketFee.Value.ToString("N0"), mainCurrency));
                            }
                            if (_settingsService?.Settings.ShowDayPrice == true && item.avg24hPrice != null && item.avg24hPrice.Value > 0)
                            {
                                sb.Append(String.Format("\nAverage 24h : {0}{1}", item.avg24hPrice.Value.ToString("N0"), mainCurrency));
                            }

                            bool sellToText = false;
                            if (_settingsService?.Settings.SellToTrader == true && item.sellFor.Count > 0)
                            {
                                List<SellFor> list = new List<SellFor>(item.sellFor);
                                list.RemoveAll(p => p.vendor.name == "Flea Market");
                                if (list.Count > 0)
                                {
                                    var sortedNoFlea = list.OrderByDescending(p => p.priceRUB).First();
                                    string vendorName = sortedNoFlea.vendor.name;

                                    if (sortedNoFlea.vendor.minTraderLevel != null)
                                        vendorName += " LL" + sortedNoFlea.vendor.minTraderLevel;

                                    if (BestSellTo_vendorName != vendorName)
                                    {
                                        string pricePerSlotDetails = GetPricePerSlotDetails(item, sortedNoFlea.priceRUB.Value, mainCurrency);
                                        sb = RemoveTrailingLineBreaks(sb);
                                        sb.Append(String.Format("\n\nSell to {0} --> {1}{2}{3}", vendorName, sortedNoFlea.priceRUB.Value.ToString("N0"), mainCurrency, pricePerSlotDetails));
                                        sellToText = true;
                                    }
                                }
                            }
                            if (_settingsService?.Settings.BuyFromTrader == true && item.buyFor.Count > 0)
                            {
                                List<BuyFor> list = new List<BuyFor>(item.buyFor);
                                list.RemoveAll(p => p.vendor.name == "Flea Market");
                                if (list.Count > 0)
                                {
                                    var sortedNoFlea = list.OrderBy(p => p.priceRUB).First();
                                    string vendorName = sortedNoFlea.vendor.name;

                                    if (sortedNoFlea.vendor.minTraderLevel != null)
                                        vendorName += " LL" + sortedNoFlea.vendor.minTraderLevel;
                                    if (BestBuyFrom_vendorName != vendorName)
                                    {
                                        sb = RemoveTrailingLineBreaks(sb);
                                        if (sellToText)
                                            sb.Append(String.Format("\n"));
                                        else
                                            sb.Append(String.Format("\n\n"));
                                        sb.Append(String.Format("Buy from {0} --> {1}{2}", vendorName, sortedNoFlea.priceRUB.Value.ToString("N0"), mainCurrency));
                                    }
                                }
                            }

                            if (_settingsService?.Settings.Needs == true && item.usedInTasks.Count > 0 && _settingsService.Settings.UseTarkovTrackerApi && item.name != "Roubles" && item.name != "Euros" && item.name != "Dollars")
                            {
                                string tasks = "";
                                int grandTotalNeeded = 0;
                                int grandTotalHave = 0;
                                var list = item.usedInTasks.OrderBy(p => p.minPlayerLevel);
                                foreach (var task in list)
                                {
                                    var trackerData = _tarkovTrackerService?.TrackerData;
                                    if (trackerData == null || trackerData.data == null)
                                        continue;

                                    // Skip tasks that are actually completed according to TarkovTracker
                                    if (trackerData.data.tasksProgress != null && trackerData.data.tasksProgress.Any(e => e.id == task.id && e.complete == true))
                                        continue;

                                    string task1 = "";
                                    if (task.minPlayerLevel != null)
                                        task1 += "[" + task.minPlayerLevel + "] ";
                                    task1 += task.name;
                                    if (task.map != null)
                                        task1 += " [" + task.map.name + "]";

                                    int totalNeededForTask = 0;
                                    int totalHaveForTask = 0;
                                    if (task.objectives != null)
                                    {
                                        foreach (var obj in task.objectives)
                                        {
                                            if (obj.type == "findItem" && obj.foundInRaid == true && obj.items != null && obj.items.Any(i => i.id == item.id))
                                            {
                                                int required = obj.count ?? 0;
                                                int needed = required;
                                                int have = 0;

                                                if (trackerData.data.taskObjectivesProgress != null && obj.id != null)
                                                {
                                                    var progress = trackerData.data.taskObjectivesProgress.FirstOrDefault(p => p.id == obj.id);
                                                    if (progress != null)
                                                    {
                                                        if (progress.complete == true)
                                                        {
                                                            needed = 0;
                                                            have = required;
                                                        }
                                                        else if (progress.count != null)
                                                        {
                                                            needed = required - progress.count.Value;
                                                            if (needed < 0) needed = 0;
                                                            have = required - needed;
                                                        }
                                                    }
                                                }

                                                if (needed < 0) needed = 0;
                                                if (have < 0) have = 0;

                                                totalNeededForTask += needed;
                                                totalHaveForTask += have;
                                            }
                                        }
                                    }

                                    int totalRequiredForTask = totalNeededForTask + totalHaveForTask;

                                    // If this task no longer needs anything (needed == 0), do not show it
                                    if (totalRequiredForTask > 0 && totalNeededForTask > 0)
                                    {
                                        // Show both what you have and what you need for this task
                                        if (totalHaveForTask > 0 || totalNeededForTask > 0)
                                        {
                                            task1 += " (" + totalHaveForTask + "/" + totalRequiredForTask + ")";
                                            if (totalNeededForTask > 0)
                                            {
                                                task1 += " (x" + totalNeededForTask + ")";
                                            }
                                        }

                                        task1 += "\n";

                                        if (!tasks.Contains(task1))
                                        {
                                            tasks += task1;
                                            grandTotalNeeded += totalNeededForTask;
                                            grandTotalHave += totalHaveForTask;
                                        }
                                    }
                                }
                                // Only show the section if there is still something pending for at least one task
                                if (tasks != "" && grandTotalNeeded > 0)
                                {
                                    sb = RemoveTrailingLineBreaks(sb);

                                    sb.Append(String.Format("\n\nUsed in Task (You have: {0}, Needed: {1}):\n{2}", grandTotalHave, grandTotalNeeded, tasks));
                                }
                            }
                            else if (_settingsService?.Settings.Needs == true && item.usedInTasks.Count > 0 && !_settingsService.Settings.UseTarkovTrackerApi && item.name != "Roubles" && item.name != "Euros" && item.name != "Dollars")
                            {
                                string tasks = "";
                                int grandTotal = 0;
                                var list = item.usedInTasks.OrderBy(p => p.minPlayerLevel);
                                foreach (var task in list)
                                {
                                    string task1 = "";
                                    if (task.minPlayerLevel != null)
                                        task1 += "[" + task.minPlayerLevel + "] ";
                                    task1 += task.name;
                                    if (task.map != null)
                                        task1 += " [" + task.map.name + "]";

                                    int totalCount = 0;
                                    if (task.objectives != null)
                                    {
                                        foreach (var obj in task.objectives)
                                        {
                                            if (obj.type == "giveItem" && obj.foundInRaid == true && obj.items != null && obj.items.Any(i => i.id == item.id))
                                            {
                                                if (obj.count != null) totalCount += obj.count.Value;
                                            }
                                        }
                                    }
                                    if (totalCount > 0)
                                    {
                                        grandTotal += totalCount;
                                        task1 += " (x" + totalCount + ")";
                                        task1 += "\n";
                                        tasks += task1;
                                    }
                                }
                                sb = RemoveTrailingLineBreaks(sb);
                                if (grandTotal > 0)
                                    sb.Append(String.Format("\n\nUsed in Task (Total: {0}):\n{1}", grandTotal, tasks));
                                else
                                    sb.Append(String.Format("\n\nUsed in Task:\n{0}", tasks));
                            }

                            // Hideout upgrades (purely local, without using API progress)
                            if (_settingsService?.Settings.ShowHideoutUpgrades == true)
                            {
                                var hideoutStations = _tarkovDataService?.Data?.hideoutStations;

                                if (item.name != "Roubles" && hideoutStations != null)
                                {
                                    var upgradesList = new List<hideoutUpgrades>();
                                    string upgrades = "";
                                    int grandTotal = 0;
                                    foreach (var station in hideoutStations)
                                    {
                                        foreach (var stationLevel in station.levels)
                                        {
                                            foreach (var itemReq in stationLevel.itemRequirements)
                                            {
                                                if (item.id == itemReq.item.id)
                                                {
                                                    int required = itemReq.count ?? 0;
                                                    int extraLocal = _tarkovTrackerService?.GetLocalHideoutExtraCount(itemReq.id) ?? 0;
                                                    int count = required - extraLocal;
                                                    if (count < 0) count = 0;

                                                    if (count > 0)
                                                    {
                                                        upgradesList.Add(new hideoutUpgrades() { Name = station.name, Level = stationLevel.level, Count = count });
                                                        grandTotal += count;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (upgradesList.Count > 0)
                                    {
                                        var sortedUpgradesList = new List<hideoutUpgrades>(upgradesList.OrderBy(p => p.Level));

                                        foreach (var upgrade in sortedUpgradesList)
                                        {
                                            upgrades += "[Level " + upgrade.Level + "] " + upgrade.Name + " (x" + upgrade.Count + ")\n";
                                        }

                                        sb = RemoveTrailingLineBreaks(sb);
                                        sb.Append(String.Format("\n\nNeeded for Hideout (Total: {0}):\n{1}", grandTotal, upgrades));
                                    }
                                }
                            }

                            //Crafts & Barters
                            if (_settingsService?.Settings.BartersAndCrafts == true)
                            {
                                string barters = "";
                                if (item.bartersFor.Count > 0)
                                {
                                    foreach (var barter in item.bartersFor)
                                    {
                                        int requiredItems = barter.requiredItems.Count;
                                        barters += "[" + barter.trader.name + " LL" + barter.trader.levels.First().level + "] ";

                                        foreach (var requiredItem in barter.requiredItems)
                                        {
                                            barters += requiredItem.item.name + " x" + requiredItem.count;
                                            requiredItems--;
                                            if (requiredItems > 0)
                                                barters += " + ";
                                            else if (requiredItems == 0)
                                                barters += " --> ";
                                        }
                                        barters += barter.rewardItems.First().item.name + " x" + barter.rewardItems.First().count + "\n";
                                    }
                                }
                                if (item.bartersUsing.Count > 0)
                                {
                                    foreach (var barter in item.bartersUsing)
                                    {
                                        int requiredItems = barter.requiredItems.Count;
                                        barters += "[" + barter.trader.name + " LL" + barter.trader.levels.First().level + "] ";

                                        foreach (var requiredItem in barter.requiredItems)
                                        {
                                            barters += requiredItem.item.name + " x" + requiredItem.count;
                                            requiredItems--;
                                            if (requiredItems > 0)
                                                barters += " + ";
                                            else if (requiredItems == 0)
                                                barters += " --> ";
                                        }
                                        barters += barter.rewardItems.First().item.name + " x" + barter.rewardItems.First().count + "\n";
                                    }
                                }
                                if (barters != "")
                                {
                                    sb = RemoveTrailingLineBreaks(sb);
                                    sb.Append(String.Format("\n\nBarters:\n{0}", barters));
                                }
                            }

                            if (_settingsService?.Settings.BartersAndCrafts == true && item.craftsFor.Count > 0)
                            {
                                string craftsForText = "";
                                foreach (var crafts in item.craftsFor)
                                {
                                    int requiredItems = crafts.requiredItems.Count;
                                    craftsForText += "[" + crafts.station.name + " L" + crafts.station.levels.First().level + "] ";

                                    foreach (var requiredItem in crafts.requiredItems)
                                    {
                                        craftsForText += requiredItem.item.name + " x" + requiredItem.count;
                                        requiredItems--;
                                        if (requiredItems > 0)
                                            craftsForText += " + ";
                                        else if (requiredItems == 0)
                                            craftsForText += " --> ";
                                    }
                                    craftsForText += crafts.rewardItems.First().item.name + " x" + crafts.rewardItems.First().count + "\n";
                                }
                                string craftsUsingText = "";
                                foreach (var crafts in item.craftsUsing)
                                {
                                    int requiredItems = crafts.requiredItems.Count;
                                    craftsUsingText += "[" + crafts.station.name + " L" + crafts.station.levels.First().level + "] ";

                                    foreach (var requiredItem in crafts.requiredItems)
                                    {
                                        craftsUsingText += requiredItem.item.name + " x" + requiredItem.count;
                                        requiredItems--;
                                        if (requiredItems > 0)
                                            craftsUsingText += " + ";
                                        else if (requiredItems == 0)
                                            craftsUsingText += " --> ";
                                    }
                                    craftsUsingText += crafts.rewardItems.First().item.name + " x" + crafts.rewardItems.First().count + "\n";
                                }
                                if (item.bartersFor.Count == 0)
                                {
                                    sb = RemoveTrailingLineBreaks(sb);
                                    sb.Append(String.Format("\n"));
                                }
                                if (craftsForText != craftsUsingText)
                                    craftsForText += craftsUsingText;
                                sb.Append(String.Format("\nCrafts:\n{0}", craftsForText));
                            }

                            if (item.types.Exists(e => e.Equals("preset")))
                            {
                                sb.Append(String.Format("\nThis is a Preset item \nCan't be sold or bought in Flea Market"));
                            }
                            else if (flea_profit == 0 && item.name != "Roubles" && item.name != "Euros" && item.name != "Dollars")
                                sb.Append(String.Format("\nItem Banned from Flea Market"));

                            iteminfo_ball.Rows.Clear();
                            iteminfo_text.Text = sb.ToString().Trim();
                            setTextColorsAPI(item);
                            if (item.ballistic != null)
                            {
                                foreach (Ballistic b in item.ballistic.Calibarlist)
                                {
                                    iteminfo_ball.Rows.Add(b.Data());
                                }
                                iteminfo_ball.Visible = true;
                                SetBallisticsColorAPI(item);
                                ResizeGrid(iteminfo_ball);
                            }
                        }
                    }
                }
            };
            Invoke(show);
        }

        public void IncrementCurrentItemCount()
        {
            try
            {
                var data = _tarkovDataService?.Data;
                if (data == null || _currentItem == null)
                {
                    return;
                }

                // Apply an immediate local increment. Try tasks first; if none, fall back to hideout (local only)
                var result = _tarkovTrackerService.ApplyLocalChangeForCurrentItem(_currentItem, data, +1);
                if (!result.Success && (result.FailureReason == TrackerUpdateFailureReason.NoObjectiveForItem || result.FailureReason == TrackerUpdateFailureReason.AlreadyCompleted))
                {
                    result = _tarkovTrackerService.ApplyLocalHideoutChangeForCurrentItem(_currentItem, data, +1);
                }

                Debug.WriteLine($"[Overlay] Tracker local update result (delta=1): Success={result.Success}, Reason={result.FailureReason}, Remaining={result.Objective?.Remaining}");

                if (result.Success)
                {
                    try
                    {
                        var token = CancellationToken.None;
                        Invoke(new Action(() => ShowInfoAPI(_currentItem, token)));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Overlay] Error while refreshing overlay after tracker update: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Overlay] Error while incrementing objective: " + ex.Message);
            }
        }

        public void DecrementCurrentItemCount()
        {
            try
            {
                var data = _tarkovDataService?.Data;
                if (data == null || _currentItem == null)
                {
                    return;
                }

                // Apply an immediate local decrement. Try tasks first; if none, fall back to hideout (local only)
                var result = _tarkovTrackerService.ApplyLocalChangeForCurrentItem(_currentItem, data, -1);
                if (!result.Success && (result.FailureReason == TrackerUpdateFailureReason.NoObjectiveForItem || result.FailureReason == TrackerUpdateFailureReason.AlreadyCompleted))
                {
                    result = _tarkovTrackerService.ApplyLocalHideoutChangeForCurrentItem(_currentItem, data, -1);
                }

                Debug.WriteLine($"[Overlay] Tracker local update result (delta=-1): Success={result.Success}, Reason={result.FailureReason}, Remaining={result.Objective?.Remaining}");

                if (result.Success)
                {
                    try
                    {
                        var token = CancellationToken.None;
                        Invoke(new Action(() => ShowInfoAPI(_currentItem, token)));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Overlay] Error while refreshing overlay after tracker update: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Overlay] Error while decrementing objective: " + ex.Message);
            }
        }

        private decimal? GetValuePerSlot(Item item, decimal? priceOverride = null)
        {
            if (item == null)
                return null;

            int? width = null;
            int? height = null;

            if (item.types != null && item.types.Exists(e => e.Equals("gun")))
            {
                if (item.properties != null && item.properties.defaultWidth != null && item.properties.defaultHeight != null)
                {
                    width = item.properties.defaultWidth;
                    height = item.properties.defaultHeight;
                }
            }

            if (width == null || height == null)
            {
                if (item.width != null && item.height != null)
                {
                    width = item.width;
                    height = item.height;
                }
            }

            if (width == null || height == null || width.Value <= 0 || height.Value <= 0)
                return null;

            int slotCount = width.Value * height.Value;
            if (slotCount <= 0)
                return null;

            decimal? fallbackPrice = item.lastLowPrice.HasValue ? (decimal?)item.lastLowPrice.Value : null;
            decimal? price = priceOverride ?? fallbackPrice;

            if (price == null || price.Value <= 0)
                return null;

            return price.Value / slotCount;
        }

        private void SetLootTierPerSlot(Item item)
        {
            if (item == null)
                return;

            string mappedTier;
            if (!string.IsNullOrEmpty(item.name) && LootTierByName.TryGetValue(item.name, out mappedTier))
            {
                item.lootTier = "[★] Loot Tier " + mappedTier + " (tarkov.dev)";
                return;
            }

            decimal? valuePerSlot = GetValuePerSlot(item);

            if (valuePerSlot == null)
            {
                item.lootTier = "No";
            }
            else if (item.types == null || !item.types.Exists(e => e.Equals("ammo")))
            {
                string slotTier = null;

                if (valuePerSlot < 8500)
                    slotTier = "F";
                else if (valuePerSlot >= 8500 && valuePerSlot < 21000)
                    slotTier = "E";
                else if (valuePerSlot >= 21000 && valuePerSlot < 26750)
                    slotTier = "D";
                else if (valuePerSlot >= 26750 && valuePerSlot < 34250)
                    slotTier = "C";
                else if (valuePerSlot >= 34250 && valuePerSlot < 45500)
                    slotTier = "B";
                else if (valuePerSlot >= 45500 && valuePerSlot < 63000)
                    slotTier = "A";
                else if (valuePerSlot >= 63000)
                    slotTier = "S";

                if (slotTier != null)
                    item.lootTier = "Loot Tier " + slotTier + " (per slot)";
            }
        }

        private string FindKeyInfo(Item item)
        {
            if (item.name.Equals("Health Resort west wing room 321 safe key"))
                return "The third floor, room 321 of the West Wing in the Health Resort on Shoreline.";
            if (item.name.Equals("Cottage back door key"))
                return "The east cottage on Shoreline.";
            if (item.name.Equals("Missam forklift key"))
                return "This key does currently not open any lock. It's a quest item only.";

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                    string html = httpClient.GetStringAsync(item.wikiLink).Result;
                    doc.LoadHtml(html);
                    HtmlAgilityPack.HtmlNode node = doc?.DocumentNode.SelectSingleNode("//div[@class='mw-parser-output']");
                    var subnode = node?.SelectSingleNode("//p[3]");
                    if (subnode != null)
                        return subnode.InnerText;
                    else
                        return null;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
        }

        public void ShowCompareAPI(Item item, CancellationToken cts_one)
        {
            Action show = delegate ()
            {
                if (!cts_one.IsCancellationRequested)
                {
                    lock (_lock)
                    {
                        DataGridViewRow temp = CheckItemExistAPI(item);
                        if (item != null && item.link != null)
                        {
                            if (temp != null)
                            {
                                ItemCompareGrid.Rows.Remove(temp);
                            }
                            ItemCompareGrid.Rows.Add(item.Data());
                            if (ItemCompareGrid.SortedColumn != null)
                            {
                                ItemCompareGrid.Sort(ItemCompareGrid.SortedColumn,
                                    ItemCompareGrid.SortOrder.Equals(SortOrder.Ascending) ? ListSortDirection.Ascending : ListSortDirection.Descending);
                            }
                            ItemCompareGrid.Visible = true;
                            ResizeGrid(ItemCompareGrid);
                        }
                        if (temp == null)
                        {
                            if (--compare_size > 0)
                            {
                                itemcompare_text.Text = String.Format("{0} Left : {1}", MainForm.languageModel == null ? Program.languageLoading : Program.waitingForTooltip, compare_size);
                            }
                            else
                            {
                                itemcompare_text.Text = String.Format("{0}", Program.presscomparekey);
                            }
                        }
                    }
                }
            };
            Invoke(show);
        }

        public DataGridViewRow CheckItemExistAPI(Item item)
        {
            DataGridViewRow value = null;
            foreach (DataGridViewRow r in ItemCompareGrid.Rows)
            {
                if ((item.name).Equals(r.Cells[0].Value))
                {
                    value = r;
                    break;
                }
            }
            return value;
        }

        public void setTextColorsAPI(Item item)
        {
            setPriceColor();
            setInraidColor();

            setOthersColorAPI(item);
            setCraftColorAPI(item);
            setLootTierColorAPI(item);
            setClassTierColorAPI(item);
        }

        public void setPriceColor()
        {
            MatchCollection mc = Program.money_filter.Matches(iteminfo_text.Text);
            foreach (Match m in mc)
            {
                iteminfo_text.Select(m.Index, m.Length);
                iteminfo_text.SelectionColor = Color.Gold;
            }
        }

        public void setInraidColor()
        {
            MatchCollection mc = Program.inraid_filter.Matches(iteminfo_text.Text);
            foreach (Match m in mc)
            {
                iteminfo_text.Select(m.Index, m.Length);
                iteminfo_text.SelectionColor = Color.Red;
            }
        }

        public void setCraftColorAPI(Item item)
        {
            // MatchCollection mc = new Regex(Regex.Escape(item.name)).Matches(iteminfo_text.Text);
            MatchCollection mc = new Regex(Regex.Escape(item.name)).Matches(iteminfo_text.Text);

            foreach (Match m in mc)
            {
                iteminfo_text.Select(m.Index, m.Length);
                iteminfo_text.SelectionColor = Color.ForestGreen;
            }
        }

        public void setLootTierColorAPI(Item item)
        {
            if (item.lootTier != null)
            {
                if (item.lootTier.Contains("F"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.lootTier)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.Gray;
                    }
                }
                else if (item.lootTier.Contains("E"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.lootTier)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.DarkGoldenrod;
                    }
                }
                else if (item.lootTier.Contains("D"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.lootTier)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.DarkGreen;
                    }
                }
                else if (item.lootTier.Contains("C"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.lootTier)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.LimeGreen;
                    }
                }
                else if (item.lootTier.Contains("B"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.lootTier)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.RoyalBlue;
                    }
                }
                else if (item.lootTier.Contains("A"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.lootTier)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.DarkViolet;
                    }
                }
                else if (item.lootTier.Contains("S"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.lootTier)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.Gold;
                    }
                }
            }
        }

        public void setClassTierColorAPI(Item item)
        {
            if (item.className != null)
            {
                if (item.className.Contains("1"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.className)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.DarkGoldenrod;
                    }
                }
                else if (item.className.Contains("2"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.className)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.LimeGreen;
                    }
                }
                else if (item.className.Contains("3"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.className)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.RoyalBlue;
                    }
                }
                else if (item.className.Contains("4"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.className)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.DarkViolet;
                    }
                }
                else if (item.className.Contains("5"))
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.className)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.Gold;
                    }
                }
                else
                {
                    MatchCollection mc = new Regex(Regex.Escape(item.className)).Matches(iteminfo_text.Text);
                    foreach (Match m in mc)
                    {
                        iteminfo_text.Select(m.Index, m.Length);
                        iteminfo_text.SelectionColor = Color.Gold;
                    }
                }
            }
        }

        public void setOthersColorAPI(Item item)
        {
            MatchCollection mc;

            string[] vendors = { "Prapor", "Therapist", "Fence", "Skier", "Peacekeeper", "Mechanic", "Ragman", "Jaeger", "Flea Market",
                                 "Workbench", "Water Collector", "Lavatory", "Medstation", "Nutrition Unit", "Intelligence Center", "Booze Generator"};
            foreach (var text in vendors)
            {
                mc = new Regex(Regex.Escape(text)).Matches(iteminfo_text.Text);
                foreach (Match m in mc)
                {
                    iteminfo_text.Select(m.Index, m.Length);
                    iteminfo_text.SelectionColor = Color.SkyBlue;
                }
            }

            string[] darkOrange = { "Barters:", "Crafts:", "Use Location:", "]", "[", "Used in Task", "Needed for Hideout", "+", "-->" };
            foreach (var text in darkOrange)
            {
                mc = new Regex(Regex.Escape(text)).Matches(iteminfo_text.Text);
                foreach (Match m in mc)
                {
                    iteminfo_text.Select(m.Index, m.Length);
                    iteminfo_text.SelectionColor = Color.DarkOrange;
                }
            }

            string[] mapList = { "Customs", "Interchange", "Factory", "Woods", "Reserve", "Shoreline", "The Lab", "Lighthouse", "Streets of Tarkov" };
            foreach (var map in mapList)
            {
                mc = new Regex(map).Matches(iteminfo_text.Text);
                foreach (Match m in mc)
                {
                    iteminfo_text.Select(m.Index, m.Length);
                    iteminfo_text.SelectionColor = Color.SteelBlue;
                }
            }

            mc = new Regex("Item Banned from Flea Market").Matches(iteminfo_text.Text);
            foreach (Match m in mc)
            {
                iteminfo_text.Select(m.Index, m.Length);
                iteminfo_text.SelectionColor = Color.Red;
            }

            mc = new Regex("This is a Preset item \nCan't be sold or bought in Flea Market").Matches(iteminfo_text.Text);
            foreach (Match m in mc)
            {
                iteminfo_text.Select(m.Index, m.Length);
                iteminfo_text.SelectionColor = Color.Orange;
            }

            mc = new Regex("WORTH").Matches(iteminfo_text.Text);
            foreach (Match m in mc)
            {
                iteminfo_text.Select(m.Index, m.Length);
                iteminfo_text.SelectionColor = Color.LimeGreen;
            }
        }

        public void ShowLoadingInfo(Point point, CancellationToken cts_one)
        {
            Action show = delegate ()
            {
                if (!cts_one.IsCancellationRequested)
                {
                    // waitinfForTooltipText = Program.waitingForTooltip;
                    iteminfo_ball.Rows.Clear();
                    iteminfo_ball.Visible = false;
                    iteminfo_text.Text = MainForm.languageModel == null ? Program.languageLoading : Program.waitingForTooltip;
                    iteminfo_panel.Location = new Point(point.X + 20, point.Y + 20);
                    iteminfo_panel.Visible = true;
                }
            };
            Invoke(show);
        }

        public void ShowWaitingForTooltipInfo(Point point, CancellationToken cts_one)
        {
            Action show = delegate ()
            {
                if (!cts_one.IsCancellationRequested)
                {
                    iteminfo_ball.Rows.Clear();
                    iteminfo_ball.Visible = false;

                    if (DotsCounter < 3)
                    {
                        DotsCounter++;
                    }
                    else
                    {
                        DotsCounter = 1;
                    }
                    waitinfForTooltipText = MainForm.languageModel == null ? Program.languageLoading : Program.waitingForTooltip;
                    for (var i = 0; i < DotsCounter; i++)
                    {
                        waitinfForTooltipText += ".";
                    }
                    iteminfo_panel.Location = new Point(point.X + 20, point.Y + 20);
                    iteminfo_text.Text = waitinfForTooltipText;
                    iteminfo_panel.Visible = true;
                }
            };
            Invoke(show);
        }

        public void ShowWaitBallistics(Point point)
        {
            Action show = delegate ()
            {
                lock (_lock)
                {
                    iteminfo_ball.Rows.Clear();
                    iteminfo_ball.Visible = false;
                    iteminfo_text.Text = Program.notfinishloading;
                    iteminfo_panel.Location = point;
                    iteminfo_panel.Visible = true;
                }
            };
            Invoke(show);
        }

        public void ShowWaitAPI(Point point)
        {
            Action show = delegate ()
            {
                lock (_lock)
                {
                    iteminfo_ball.Rows.Clear();
                    iteminfo_ball.Visible = false;
                    iteminfo_text.Text = Program.notfinishloadingAPI;
                    iteminfo_panel.Location = point;
                    iteminfo_panel.Visible = true;
                }
            };
            Invoke(show);
        }

        public void ShowLoadingCompare(Point point, CancellationToken cts_one)
        {
            Action show = delegate ()
            {
                if (!cts_one.IsCancellationRequested)
                {
                    lock (_lock)
                    {
                        if (!itemcompare_panel.Visible)
                        {
                            compare_size = 0;
                            ItemCompareGrid.Rows.Clear();
                            ResizeGrid(ItemCompareGrid);
                            itemcompare_panel.Location = point;
                            itemcompare_panel.Visible = true;
                            itemcompare_text.Text = String.Format("{0}", Program.presscomparekey);
                        }
                        itemcompare_text.Text = String.Format("{0} Left : {1}", MainForm.languageModel == null ? Program.languageLoading : Program.waitingForTooltip, ++compare_size);
                    }
                }
            };
            Invoke(show);
        }

        public void HideInfo()
        {
            Action show = delegate ()
            {
                iteminfo_ball.Visible = false;
                iteminfo_panel.Visible = false;
            };
            Invoke(show);
        }

        public void HideCompare()
        {
            Action show = delegate ()
            {
                ItemCompareGrid.Visible = false;
                itemcompare_panel.Visible = false;
            };
            Invoke(show);
        }

        public void ChangeTransparent(int value)
        {
            Action show = delegate ()
            {
                this.Opacity = value * 0.01;
            };
            Invoke(show);
        }

        private void FixLocation(Control p)
        {
            int totalwidth = p.Location.X + p.Width;
            int totalheight = p.Location.Y + p.Height;
            int x = p.Location.X;
            int y = p.Location.Y;
            if (totalwidth > this.Width)
            {
                x -= totalwidth - this.Width;
            }
            if (totalheight > this.Height)
            {
                y -= totalheight - this.Height;
            }
            if (x != p.Location.X || y != p.Location.Y)
            {
                p.Location = new Point(x, y);
            }
            p.Refresh();
        }

        private void itemwindow_panel_Paint(object sender, PaintEventArgs e)
        {
            ControlPaint.DrawBorder(e.Graphics, (sender as Control).ClientRectangle, Color.White, ButtonBorderStyle.Solid);
        }

        private void itemwindow_panel_SizeChanged(object sender, EventArgs e)
        {
            iteminfo_ball.Location = new Point(iteminfo_text.Location.X, iteminfo_text.Location.Y + iteminfo_text.Height + 15);
            FixLocation(sender as Control);
        }

        private void itemwindow_panel_LocationChanged(object sender, EventArgs e)
        {
            FixLocation(sender as Control);
        }

        private void Overlay_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        private void itemwindow_text_ContentsResized(object sender, ContentsResizedEventArgs e)
        {
            (sender as Control).ClientSize = new Size(e.NewRectangle.Width + 1, e.NewRectangle.Height + 1);
        }

        private void itemcompare_text_MouseMove(object sender, MouseEventArgs e)
        {
            if (ismoving)
            {
                itemcompare_panel.Location = new Point(Cursor.Position.X - x, Cursor.Position.Y - y);
            }
        }

        private void itemcompare_text_MouseDown(object sender, MouseEventArgs e)
        {
            x = Cursor.Position.X - itemcompare_panel.Location.X;
            y = Cursor.Position.Y - itemcompare_panel.Location.Y;
            ismoving = true;
        }

        private void itemcompare_text_MouseUp(object sender, MouseEventArgs e)
        {
            ismoving = false;
        }

        public static StringBuilder RemoveTrailingLineBreaks(StringBuilder input)
        {
            while (input.Length > 0 && input[input.Length - 1] == '\n')
            {
                input.Remove(input.Length - 1, 1);
            }

            return input;
        }

        private string GetPricePerSlotDetails(Item item, int vendorPrice, string mainCurrency)
        {
            if (item == null || vendorPrice <= 0)
                return string.Empty;

            decimal? pricePerSlot = GetValuePerSlot(item, vendorPrice);
            if (!pricePerSlot.HasValue)
                return string.Empty;

            string worthSuffix = pricePerSlot.Value >= Program.GetWorthPerSlotThreshold() ? " WORTH" : string.Empty;

            return String.Format(" ({0}{1}/slot){2}", pricePerSlot.Value.ToString("N0"), mainCurrency, worthSuffix);
        }
    }

    public class hideoutUpgrades
    {
        public string Name { get; set; }
        public int? Level { get; set; }
        public int Count { get; set; }
    }
}
