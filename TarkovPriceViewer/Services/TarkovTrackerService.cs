using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TarkovPriceViewer.Configuration;
using TarkovPriceViewer.Models;
using static TarkovPriceViewer.Models.TarkovDevAPI;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovTrackerService
    {
        TarkovTrackerAPI.Root TrackerData { get; }
        bool IsLoaded { get; }
        Task UpdateTarkovTrackerAPI(bool force = false);

        CurrentTrackedObjective GetCurrentTrackedObjectiveForItem(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData);
        CurrentTrackedObjective GetCurrentHideoutRequirementForItem(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData);
        TrackerUpdateResult TryIncrementCurrentObjectiveForCurrentItem(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData);
        TrackerUpdateResult TryDecrementCurrentObjectiveForCurrentItem(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData);

        TrackerUpdateResult TryChangeCurrentObjectiveForCurrentItem(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData, int delta);

        TrackerUpdateResult ApplyLocalChangeForCurrentItem(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData, int delta);

        TrackerUpdateResult ApplyLocalHideoutChangeForCurrentItem(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData, int delta);

        int GetLocalHideoutExtraCount(string requirementId);

        Task<TrackerUpdateResult> IncrementObjectiveAndSyncAsync(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData, CancellationToken cancellationToken = default);
        Task<TrackerUpdateResult> DecrementObjectiveAndSyncAsync(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData, CancellationToken cancellationToken = default);
        Task<TrackerUpdateResult> ChangeObjectiveAndSyncAsync(TarkovDevAPI.Item item, TarkovDevAPI.Data tarkovData, int delta, CancellationToken cancellationToken = default);
    }

    public class TarkovTrackerService : ITarkovTrackerService, IDisposable
    {
        private const string LocalTasksFile = "tarkovtracker-tasks.json";
        private const string LocalHideoutFile = "tarkovtracker-hideout.json";
        private const string TarkovTrackerBaseUrl = "https://tarkovtracker.org/api/v2";

        private static readonly TimeSpan TooManyRequestsCooldown = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsService _settingsService;
        private readonly ITarkovDataService _tarkovDataService;
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, CurrentTrackedObjective> _pendingTaskObjectives = new Dictionary<string, CurrentTrackedObjective>();
        private readonly Dictionary<string, int> _localHideout = new Dictionary<string, int>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private DateTime _lastTooManyRequests = DateTime.MinValue;

        public TarkovTrackerAPI.Root TrackerData { get; private set; }
        public bool IsLoaded { get; private set; }
        public DateTime LastUpdated { get; private set; } = DateTime.Now.AddHours(-5);

        public TarkovTrackerService(IHttpClientFactory httpClientFactory, ISettingsService settingsService, ITarkovDataService tarkovDataService)
        {
            _httpClientFactory = httpClientFactory;
            _settingsService = settingsService;
            _tarkovDataService = tarkovDataService;

            LoadLocalTasksState();
            LoadLocalHideoutState();

            Task.Run(() => FlushLoopAsync(_cts.Token));
        }

        public int GetLocalHideoutExtraCount(string requirementId)
        {
            if (string.IsNullOrEmpty(requirementId))
            {
                return 0;
            }

            lock (_lockObject)
            {
                return _localHideout.TryGetValue(requirementId, out int value) ? value : 0;
            }
        }

        private void ApplyLocalObjectiveUpdate(CurrentTrackedObjective updatedObjective)
        {
            if (updatedObjective == null || updatedObjective.ObjectiveId == null)
            {
                return;
            }

            try
            {
                lock (_lockObject)
                {
                    var trackerData = TrackerData?.data;
                    if (trackerData == null || trackerData.taskObjectivesProgress == null)
                    {
                        return;
                    }

                    var progress = trackerData.taskObjectivesProgress.FirstOrDefault(p => p.id == updatedObjective.ObjectiveId);
                    if (progress == null)
                    {
                        progress = new TarkovTrackerAPI.TaskObjectivesProgress
                        {
                            id = updatedObjective.ObjectiveId
                        };
                        trackerData.taskObjectivesProgress.Add(progress);
                    }

                    progress.count = updatedObjective.CurrentCount;
                    progress.complete = updatedObjective.CurrentCount >= updatedObjective.RequiredCount;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovTrackerService.ApplyLocalObjectiveUpdate", "Error while applying local objective update", ex);
            }
        }

        private async Task FlushLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(FlushInterval, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    await FlushPendingObjectivesAsync(token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("TarkovTrackerService.FlushLoopAsync", "Error in FlushLoopAsync", ex);
                }
            }
        }

        private async Task FlushPendingObjectivesAsync(CancellationToken cancellationToken)
        {
            List<CurrentTrackedObjective> snapshot;

            lock (_lockObject)
            {
                if (_pendingTaskObjectives.Count == 0)
                {
                    return;
                }

                snapshot = _pendingTaskObjectives.Values.ToList();
                _pendingTaskObjectives.Clear();
            }

            foreach (CurrentTrackedObjective objective in snapshot)
            {
                try
                {
                    if (!await UpdateObjectiveCountAsync(objective, cancellationToken).ConfigureAwait(false))
                    {
                        lock (_lockObject)
                        {
                            _pendingTaskObjectives[objective.ObjectiveId] = objective;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error("TarkovTrackerService.FlushPendingObjectivesAsync", "Error while flushing objective", ex);
                    lock (_lockObject)
                    {
                        _pendingTaskObjectives[objective.ObjectiveId] = objective;
                    }
                }
            }

            SaveLocalTasksState();
            SaveLocalHideoutState();
        }

        public async Task UpdateTarkovTrackerAPI(bool force = false)
        {
            AppSettings settings = _settingsService.Settings;
            string apiKey = settings.TarkovTrackerApiKey;

            if (settings.UseTarkovTrackerApi && !string.Equals(apiKey, "APIKey") && !string.IsNullOrWhiteSpace(apiKey))
            {
                if (force || (DateTime.Now - LastUpdated).TotalSeconds >= 30)
                {
                    try
                    {
                        AppLogger.Info("TarkovTrackerService.UpdateTarkovTrackerAPI", "Updating TarkovTracker API...");

                        HttpClient client = _httpClientFactory.CreateClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                        HttpResponseMessage httpResponse = await client.GetAsync($"{TarkovTrackerBaseUrl}/progress");
                        if (httpResponse.IsSuccessStatusCode)
                        {
                            string responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                            List<CurrentTrackedObjective> pendingSnapshot;

                            lock (_lockObject)
                            {
                                TrackerData = JsonConvert.DeserializeObject<TarkovTrackerAPI.Root>(responseContent);
                                LastUpdated = DateTime.Now;
                                IsLoaded = true;
                                AppLogger.Info("TarkovTrackerService.UpdateTarkovTrackerAPI", "TarkovTracker API Updated");

                                pendingSnapshot = _pendingTaskObjectives.Values.ToList();
                            }

                            foreach (CurrentTrackedObjective obj in pendingSnapshot)
                            {
                                ApplyLocalObjectiveUpdate(obj);
                            }
                        }
                        else
                        {
                            AppLogger.Warn("TarkovTrackerService.UpdateTarkovTrackerAPI", $"Failed to GET /progress: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("TarkovTrackerService.UpdateTarkovTrackerAPI", "Error trying to update TarkovTracker API", ex);
                    }
                }
                else
                {
                    AppLogger.Info("TarkovTrackerService.UpdateTarkovTrackerAPI", "No need to update TarkovTracker API");
                }
            }
            else
            {
                AppLogger.Info("TarkovTrackerService.UpdateTarkovTrackerAPI", "Skipping update: API usage disabled or API key not set.");
            }
        }

        private async Task<bool> UpdateObjectiveCountAsync(CurrentTrackedObjective updatedObjective, CancellationToken cancellationToken)
        {
            AppSettings settings = _settingsService.Settings;
            string apiKey = settings.TarkovTrackerApiKey;

            if (!settings.UseTarkovTrackerApi || string.Equals(apiKey, "APIKey") || string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }

            if (DateTime.UtcNow - _lastTooManyRequests < TooManyRequestsCooldown)
            {
                AppLogger.Warn("TarkovTrackerService.UpdateObjectiveCountAsync", "Skipping objective update due to recent 429 (cooldown in effect)");
                return false;
            }

            try
            {
                HttpClient client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                string state = updatedObjective.CurrentCount >= updatedObjective.RequiredCount ? "completed" : "uncompleted";
                var payload = new { count = updatedObjective.CurrentCount, state };
                string json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{TarkovTrackerBaseUrl}/progress/task/objective/{updatedObjective.ObjectiveId}";
                AppLogger.Info("TarkovTrackerService.UpdateObjectiveCountAsync", $"Updating objective via {url} -> count={updatedObjective.CurrentCount}, state={state}");

                using HttpResponseMessage response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    AppLogger.Warn("TarkovTrackerService.UpdateObjectiveCountAsync", $"Failed to update objective {updatedObjective.ObjectiveId}: {(int)response.StatusCode} {response.ReasonPhrase}");

                    if ((int)response.StatusCode == 429)
                    {
                        _lastTooManyRequests = DateTime.UtcNow;
                    }

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovTrackerService.UpdateObjectiveCountAsync", "Error while updating objective", ex);
                return false;
            }
        }

        public CurrentTrackedObjective GetCurrentTrackedObjectiveForItem(Item item, Data tarkovData)
        {
            return SelectTaskObjectiveForDelta(item, tarkovData, +1);
        }

        public CurrentTrackedObjective GetCurrentHideoutRequirementForItem(Item item, Data tarkovData)
        {
            if (item == null || tarkovData == null)
            {
                return null;
            }

            var hideoutStations = tarkovData.hideoutStations;
            if (hideoutStations == null)
            {
                return null;
            }

            CurrentTrackedObjective best = null;
            int bestLevel = int.MinValue;

            foreach (HideoutStation station in hideoutStations)
            {
                if (station.levels == null)
                {
                    continue;
                }

                foreach (Level stationLevel in station.levels)
                {
                    if (stationLevel.itemRequirements == null)
                    {
                        continue;
                    }

                    foreach (ItemRequirement itemReq in stationLevel.itemRequirements)
                    {
                        if (itemReq.item == null || itemReq.item.id != item.id)
                        {
                            continue;
                        }

                        int required = itemReq.count ?? 0;
                        int extraLocal = GetLocalHideoutExtraCount(itemReq.id);
                        int current = extraLocal;
                        if (current > required)
                        {
                            current = required;
                        }

                        int levelValue = stationLevel.level ?? int.MaxValue;
                        if (best == null || levelValue < bestLevel)
                        {
                            bestLevel = levelValue;
                            best = new CurrentTrackedObjective
                            {
                                ObjectiveId = itemReq.id,
                                ItemId = item.id,
                                RequiredCount = required,
                                CurrentCount = current
                            };
                        }
                    }
                }
            }

            return best;
        }

        public TrackerUpdateResult TryIncrementCurrentObjectiveForCurrentItem(Item item, Data tarkovData)
        {
            return TryChangeCurrentObjectiveForCurrentItem(item, tarkovData, +1);
        }

        public TrackerUpdateResult TryDecrementCurrentObjectiveForCurrentItem(Item item, Data tarkovData)
        {
            return TryChangeCurrentObjectiveForCurrentItem(item, tarkovData, -1);
        }

        public TrackerUpdateResult TryChangeCurrentObjectiveForCurrentItem(Item item, Data tarkovData, int delta)
        {
            if (delta == 0)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoObjectiveForItem);
            }

            CurrentTrackedObjective objective = SelectTaskObjectiveForDelta(item, tarkovData, delta);
            if (objective == null)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoObjectiveForItem);
            }

            int newCount = objective.CurrentCount + delta;
            if (newCount < 0)
            {
                newCount = 0;
            }

            if (newCount > objective.RequiredCount)
            {
                newCount = objective.RequiredCount;
            }

            if (newCount == objective.CurrentCount)
            {
                if (delta > 0 && objective.CurrentCount >= objective.RequiredCount)
                {
                    return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.AlreadyCompleted, objective);
                }

                if (delta < 0 && objective.CurrentCount <= 0)
                {
                    return TrackerUpdateResult.Ok(objective);
                }
            }

            var updated = new CurrentTrackedObjective
            {
                ObjectiveId = objective.ObjectiveId,
                ItemId = objective.ItemId,
                RequiredCount = objective.RequiredCount,
                CurrentCount = newCount
            };

            AppLogger.Info("TarkovTrackerService.TryChangeCurrentObjectiveForCurrentItem", $"Change objective {updated.ObjectiveId} for item {updated.ItemId}: {objective.CurrentCount} -> {updated.CurrentCount} (delta={delta})");
            return TrackerUpdateResult.Ok(updated);
        }

        private CurrentTrackedObjective SelectTaskObjectiveForDelta(Item item, Data tarkovData, int delta)
        {
            if (item == null || tarkovData == null || TrackerData == null || TrackerData.data == null)
            {
                return null;
            }

            TarkovTrackerAPI.Data trackerData = TrackerData.data;
            var candidates = GetOrderedTaskObjectivesForItem(item, trackerData);
            if (candidates.Count == 0)
            {
                return null;
            }

            if (delta > 0)
            {
                foreach (var c in candidates)
                {
                    if (c.current < c.required)
                    {
                        return new CurrentTrackedObjective
                        {
                            ObjectiveId = c.objectiveId,
                            ItemId = item.id,
                            RequiredCount = c.required,
                            CurrentCount = c.current
                        };
                    }
                }
            }
            else
            {
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    var c = candidates[i];
                    if (c.current > 0)
                    {
                        return new CurrentTrackedObjective
                        {
                            ObjectiveId = c.objectiveId,
                            ItemId = item.id,
                            RequiredCount = c.required,
                            CurrentCount = c.current
                        };
                    }
                }
            }

            return null;
        }

        private List<(string objectiveId, int required, int current)> GetOrderedTaskObjectivesForItem(Item item, TarkovTrackerAPI.Data trackerData)
        {
            var result = new List<(string objectiveId, int required, int current)>();

            var usedInTasks = item.usedInTasks;
            if (usedInTasks == null || usedInTasks.Count == 0)
            {
                return result;
            }

            var orderedTasks = usedInTasks
                .Where(t => t.objectives != null)
                .OrderBy(t => t.minPlayerLevel ?? int.MaxValue)
                .ToList();

            foreach (var task in orderedTasks)
            {
                if (task.objectives == null)
                {
                    continue;
                }

                foreach (var obj in task.objectives)
                {
                    if (obj.type != "giveItem" || obj.foundInRaid != true || obj.items == null || !obj.items.Any(i => i.id == item.id))
                    {
                        continue;
                    }

                    int required = obj.count ?? 0;
                    int current = 0;

                    if (trackerData.taskObjectivesProgress != null && obj.id != null)
                    {
                        var progress = trackerData.taskObjectivesProgress.FirstOrDefault(p => p.id == obj.id);
                        if (progress != null)
                        {
                            if (progress.complete == true)
                            {
                                current = required;
                            }
                            else if (progress.count != null)
                            {
                                current = progress.count.Value;
                            }
                        }
                    }

                    result.Add((obj.id, required, current));
                }
            }

            return result;
        }

        public TrackerUpdateResult ApplyLocalChangeForCurrentItem(Item item, Data tarkovData, int delta)
        {
            TrackerUpdateResult validation = TryChangeCurrentObjectiveForCurrentItem(item, tarkovData, delta);
            if (!validation.Success)
            {
                return validation;
            }

            CurrentTrackedObjective updatedObjective = validation.Objective;

            ApplyLocalObjectiveUpdate(updatedObjective);

            lock (_lockObject)
            {
                if (!string.IsNullOrEmpty(updatedObjective.ObjectiveId))
                {
                    _pendingTaskObjectives[updatedObjective.ObjectiveId] = updatedObjective;
                }
            }

            SaveLocalTasksState();

            return validation;
        }

        public TrackerUpdateResult ApplyLocalHideoutChangeForCurrentItem(Item item, Data tarkovData, int delta)
        {
            if (delta == 0)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoObjectiveForItem);
            }

            CurrentTrackedObjective requirement = SelectHideoutRequirementForDelta(item, tarkovData, delta);
            if (requirement == null || string.IsNullOrEmpty(requirement.ObjectiveId))
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoObjectiveForItem);
            }

            int newCount = requirement.CurrentCount + delta;
            if (newCount < 0)
            {
                newCount = 0;
            }

            if (newCount > requirement.RequiredCount)
            {
                newCount = requirement.RequiredCount;
            }

            if (newCount == requirement.CurrentCount)
            {
                if (delta > 0 && requirement.CurrentCount >= requirement.RequiredCount)
                {
                    return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.AlreadyCompleted, requirement);
                }

                if (delta < 0 && requirement.CurrentCount <= 0)
                {
                    return TrackerUpdateResult.Ok(requirement);
                }
            }

            int newExtra = newCount;

            lock (_lockObject)
            {
                _localHideout[requirement.ObjectiveId] = newExtra;
            }

            var updated = new CurrentTrackedObjective
            {
                ObjectiveId = requirement.ObjectiveId,
                ItemId = requirement.ItemId,
                RequiredCount = requirement.RequiredCount,
                CurrentCount = newCount
            };

            AppLogger.Info("TarkovTrackerService.ApplyLocalHideoutChangeForCurrentItem", $"Local hideout change {updated.ObjectiveId} for item {updated.ItemId}: {requirement.CurrentCount} -> {updated.CurrentCount} (delta={delta})");

            SaveLocalHideoutState();
            return TrackerUpdateResult.Ok(updated);
        }

        private class LocalObjectiveState
        {
            public string ObjectiveId { get; set; }
            public string ItemId { get; set; }
            public int RequiredCount { get; set; }
            public int CurrentCount { get; set; }
        }

        private class LocalHideoutRequirementState
        {
            public string RequirementId { get; set; }
            public int Count { get; set; }
        }

        private void SaveLocalTasksState()
        {
            try
            {
                List<LocalObjectiveState> objectives;
                TarkovDevAPI.Data tarkovDataSnapshot = _tarkovDataService?.Data;

                lock (_lockObject)
                {
                    objectives = _pendingTaskObjectives.Values
                        .Select(o => new LocalObjectiveState
                        {
                            ObjectiveId = o.ObjectiveId,
                            ItemId = o.ItemId,
                            RequiredCount = o.RequiredCount,
                            CurrentCount = o.CurrentCount
                        })
                        .ToList();
                }

                var objectiveInfo = new Dictionary<string, (string TaskName, string ObjectiveDescription)>();
                var itemNames = new Dictionary<string, string>();

                if (tarkovDataSnapshot?.items != null)
                {
                    foreach (Item item in tarkovDataSnapshot.items)
                    {
                        if (!string.IsNullOrEmpty(item.id) && !itemNames.ContainsKey(item.id))
                        {
                            itemNames[item.id] = item.name;
                        }

                        if (item.usedInTasks == null)
                        {
                            continue;
                        }

                        foreach (UsedInTask task in item.usedInTasks)
                        {
                            if (task.objectives == null)
                            {
                                continue;
                            }

                            foreach (Objective obj in task.objectives)
                            {
                                if (string.IsNullOrEmpty(obj.id))
                                {
                                    continue;
                                }

                                if (!objectiveInfo.ContainsKey(obj.id))
                                {
                                    objectiveInfo[obj.id] = (task.name, obj.description);
                                }
                            }
                        }
                    }
                }

                var enrichedObjectives = objectives
                    .Select(o =>
                    {
                        objectiveInfo.TryGetValue(o.ObjectiveId, out var info);
                        itemNames.TryGetValue(o.ItemId, out string itemName);

                        return new
                        {
                            o.ObjectiveId,
                            o.ItemId,
                            o.RequiredCount,
                            o.CurrentCount,
                            TaskName = info.TaskName,
                            ObjectiveDescription = info.ObjectiveDescription,
                            ItemName = itemName
                        };
                    })
                    .ToList();

                string json = JsonConvert.SerializeObject(new { Objectives = enrichedObjectives }, Formatting.Indented);
                File.WriteAllText(LocalTasksFile, json);
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovTrackerService.SaveLocalTasksState", "Error while saving local tasks state", ex);
            }
        }

        private void SaveLocalHideoutState()
        {
            try
            {
                List<LocalHideoutRequirementState> requirements;
                TarkovDevAPI.Data tarkovDataSnapshot = _tarkovDataService?.Data;

                lock (_lockObject)
                {
                    var keysToRemove = _localHideout.Where(kvp => kvp.Value <= 0).Select(kvp => kvp.Key).ToList();
                    foreach (string key in keysToRemove)
                    {
                        _localHideout.Remove(key);
                    }

                    requirements = _localHideout
                        .Where(kvp => kvp.Value > 0)
                        .Select(kvp => new LocalHideoutRequirementState
                        {
                            RequirementId = kvp.Key,
                            Count = kvp.Value
                        })
                        .ToList();
                }

                var hideoutInfo = new Dictionary<string, (string StationName, int? StationLevel, string ItemId, string ItemName, int RequiredCount)>();

                if (tarkovDataSnapshot?.hideoutStations != null)
                {
                    foreach (HideoutStation station in tarkovDataSnapshot.hideoutStations)
                    {
                        if (station.levels == null)
                        {
                            continue;
                        }

                        foreach (Level level in station.levels)
                        {
                            if (level.itemRequirements == null)
                            {
                                continue;
                            }

                            foreach (ItemRequirement req in level.itemRequirements)
                            {
                                if (string.IsNullOrEmpty(req.id))
                                {
                                    continue;
                                }

                                string itemId = req.item?.id;
                                string itemName = req.item?.name;
                                int required = req.count ?? 0;

                                hideoutInfo[req.id] = (station.name, level.level, itemId, itemName, required);
                            }
                        }
                    }
                }

                var enrichedRequirements = requirements
                    .Select(r =>
                    {
                        hideoutInfo.TryGetValue(r.RequirementId, out var info);

                        return new
                        {
                            r.RequirementId,
                            r.Count,
                            info.StationName,
                            info.StationLevel,
                            info.ItemId,
                            info.ItemName,
                            info.RequiredCount
                        };
                    })
                    .ToList();

                string json = JsonConvert.SerializeObject(new { Requirements = enrichedRequirements }, Formatting.Indented);
                File.WriteAllText(LocalHideoutFile, json);
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovTrackerService.SaveLocalHideoutState", "Error while saving local hideout state", ex);
            }
        }

        private void LoadLocalTasksState()
        {
            try
            {
                if (!File.Exists(LocalTasksFile))
                {
                    return;
                }

                string json = File.ReadAllText(LocalTasksFile);
                dynamic wrapper = JsonConvert.DeserializeObject(json);
                dynamic objectivesToken = wrapper?.Objectives;
                if (objectivesToken == null)
                {
                    return;
                }

                var objectives = objectivesToken.ToObject<List<LocalObjectiveState>>();
                if (objectives == null)
                {
                    return;
                }

                lock (_lockObject)
                {
                    _pendingTaskObjectives.Clear();
                    foreach (LocalObjectiveState o in objectives)
                    {
                        if (string.IsNullOrEmpty(o.ObjectiveId))
                        {
                            continue;
                        }

                        _pendingTaskObjectives[o.ObjectiveId] = new CurrentTrackedObjective
                        {
                            ObjectiveId = o.ObjectiveId,
                            ItemId = o.ItemId,
                            RequiredCount = o.RequiredCount,
                            CurrentCount = o.CurrentCount
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovTrackerService.LoadLocalTasksState", "Error while loading local tasks state", ex);
            }
        }

        private void LoadLocalHideoutState()
        {
            try
            {
                if (!File.Exists(LocalHideoutFile))
                {
                    return;
                }

                string json = File.ReadAllText(LocalHideoutFile);
                dynamic wrapper = JsonConvert.DeserializeObject(json);
                dynamic requirementsToken = wrapper?.Requirements;
                if (requirementsToken == null)
                {
                    return;
                }

                var requirements = requirementsToken.ToObject<List<LocalHideoutRequirementState>>();
                if (requirements == null)
                {
                    return;
                }

                lock (_lockObject)
                {
                    _localHideout.Clear();
                    foreach (LocalHideoutRequirementState r in requirements)
                    {
                        if (string.IsNullOrEmpty(r.RequirementId))
                        {
                            continue;
                        }

                        if (r.Count <= 0)
                        {
                            continue;
                        }

                        _localHideout[r.RequirementId] = r.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("TarkovTrackerService.LoadLocalHideoutState", "Error while loading local hideout state", ex);
            }
        }

        private CurrentTrackedObjective SelectHideoutRequirementForDelta(Item item, Data tarkovData, int delta)
        {
            if (item == null || tarkovData == null)
            {
                return null;
            }

            var hideoutStations = tarkovData.hideoutStations;
            if (hideoutStations == null)
            {
                return null;
            }

            var requirements = new List<(int level, string requirementId, int required, int current)>();

            foreach (HideoutStation station in hideoutStations)
            {
                if (station.levels == null)
                {
                    continue;
                }

                foreach (Level stationLevel in station.levels)
                {
                    if (stationLevel.itemRequirements == null)
                    {
                        continue;
                    }

                    foreach (ItemRequirement itemReq in stationLevel.itemRequirements)
                    {
                        if (itemReq.item == null || itemReq.item.id != item.id)
                        {
                            continue;
                        }

                        int required = itemReq.count ?? 0;
                        int extraLocal = GetLocalHideoutExtraCount(itemReq.id);
                        int current = extraLocal;
                        if (current > required)
                        {
                            current = required;
                        }

                        int levelValue = stationLevel.level ?? int.MaxValue;
                        requirements.Add((levelValue, itemReq.id, required, current));
                    }
                }
            }

            if (requirements.Count == 0)
            {
                return null;
            }

            var ordered = requirements.OrderBy(r => r.level).ToList();

            if (delta > 0)
            {
                foreach (var r in ordered)
                {
                    if (r.current < r.required)
                    {
                        return new CurrentTrackedObjective
                        {
                            ObjectiveId = r.requirementId,
                            ItemId = item.id,
                            RequiredCount = r.required,
                            CurrentCount = r.current
                        };
                    }
                }
            }
            else
            {
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    var r = ordered[i];
                    if (r.current > 0)
                    {
                        return new CurrentTrackedObjective
                        {
                            ObjectiveId = r.requirementId,
                            ItemId = item.id,
                            RequiredCount = r.required,
                            CurrentCount = r.current
                        };
                    }
                }
            }

            return null;
        }

        public async Task<TrackerUpdateResult> IncrementObjectiveAndSyncAsync(Item item, Data tarkovData, CancellationToken cancellationToken = default)
        {
            TrackerUpdateResult validation = TryIncrementCurrentObjectiveForCurrentItem(item, tarkovData);
            if (!validation.Success)
            {
                return validation;
            }

            CurrentTrackedObjective updatedObjective = validation.Objective;
            if (!await UpdateObjectiveCountAsync(updatedObjective, cancellationToken).ConfigureAwait(false))
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.ApiError, updatedObjective);
            }

            ApplyLocalObjectiveUpdate(updatedObjective);
            return validation;
        }

        public async Task<TrackerUpdateResult> DecrementObjectiveAndSyncAsync(Item item, Data tarkovData, CancellationToken cancellationToken = default)
        {
            TrackerUpdateResult validation = TryDecrementCurrentObjectiveForCurrentItem(item, tarkovData);
            if (!validation.Success)
            {
                return validation;
            }

            CurrentTrackedObjective updatedObjective = validation.Objective;
            if (!await UpdateObjectiveCountAsync(updatedObjective, cancellationToken).ConfigureAwait(false))
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.ApiError, updatedObjective);
            }

            ApplyLocalObjectiveUpdate(updatedObjective);
            return validation;
        }

        public async Task<TrackerUpdateResult> ChangeObjectiveAndSyncAsync(Item item, Data tarkovData, int delta, CancellationToken cancellationToken = default)
        {
            TrackerUpdateResult validation = TryChangeCurrentObjectiveForCurrentItem(item, tarkovData, delta);
            if (!validation.Success)
            {
                return validation;
            }

            CurrentTrackedObjective updatedObjective = validation.Objective;
            if (!await UpdateObjectiveCountAsync(updatedObjective, cancellationToken).ConfigureAwait(false))
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.ApiError, updatedObjective);
            }

            ApplyLocalObjectiveUpdate(updatedObjective);
            return validation;
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }
}
