using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TarkovPriceViewer.Models;
using System.Diagnostics;
using System.Linq;
using static TarkovPriceViewer.Models.TarkovAPI;
using System.Collections.Generic;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovTrackerService
    {
        TarkovTrackerAPI.Root TrackerData { get; }
        bool IsLoaded { get; }
        Task UpdateTarkovTrackerAPI(bool force = false);

        CurrentTrackedObjective GetCurrentTrackedObjectiveForItem(TarkovAPI.Item item, TarkovAPI.Data tarkovData);
        TrackerUpdateResult TryIncrementCurrentObjectiveForCurrentItem(TarkovAPI.Item item, TarkovAPI.Data tarkovData);
        TrackerUpdateResult TryDecrementCurrentObjectiveForCurrentItem(TarkovAPI.Item item, TarkovAPI.Data tarkovData);

        TrackerUpdateResult TryChangeCurrentObjectiveForCurrentItem(TarkovAPI.Item item, TarkovAPI.Data tarkovData, int delta);

        // Aplica un cambio solo en memoria (caché local) y encola el objetivo para flush posterior
        TrackerUpdateResult ApplyLocalChangeForCurrentItem(TarkovAPI.Item item, TarkovAPI.Data tarkovData, int delta);

        Task<TrackerUpdateResult> IncrementObjectiveAndSyncAsync(TarkovAPI.Item item, TarkovAPI.Data tarkovData, CancellationToken cancellationToken = default);
        Task<TrackerUpdateResult> DecrementObjectiveAndSyncAsync(TarkovAPI.Item item, TarkovAPI.Data tarkovData, CancellationToken cancellationToken = default);
        Task<TrackerUpdateResult> ChangeObjectiveAndSyncAsync(TarkovAPI.Item item, TarkovAPI.Data tarkovData, int delta, CancellationToken cancellationToken = default);
    }

    public class TarkovTrackerService : ITarkovTrackerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISettingsService _settingsService;
        
        public TarkovTrackerAPI.Root TrackerData { get; private set; }
        public bool IsLoaded { get; private set; }
        public DateTime LastUpdated { get; private set; } = DateTime.Now.AddHours(-5);
        private readonly object _lockObject = new object();

        private DateTime _lastTooManyRequests = DateTime.MinValue;
        private static readonly TimeSpan TooManyRequestsCooldown = TimeSpan.FromSeconds(5);

        private readonly Dictionary<string, CurrentTrackedObjective> _pendingObjectives = new Dictionary<string, CurrentTrackedObjective>();
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

        public TarkovTrackerService(IHttpClientFactory httpClientFactory, ISettingsService settingsService)
        {
            _httpClientFactory = httpClientFactory;
            _settingsService = settingsService;

            // Lanzar un bucle en background que haga flush periódico de los cambios locales a la API
            Task.Run(FlushLoopAsync);
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
                        return;

                    var progress = trackerData.taskObjectivesProgress.FirstOrDefault(p => p.id == updatedObjective.ObjectiveId);
                    if (progress != null)
                    {
                        progress.count = updatedObjective.CurrentCount;
                        progress.complete = updatedObjective.CurrentCount >= updatedObjective.RequiredCount;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[TarkovTracker] Error while applying local objective update: " + ex.Message);
            }
        }

        private async Task FlushLoopAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(FlushInterval).ConfigureAwait(false);
                    await FlushPendingObjectivesAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[TarkovTracker] Error in FlushLoopAsync: " + ex.Message);
                }
            }
        }

        private async Task FlushPendingObjectivesAsync(CancellationToken cancellationToken)
        {
            List<CurrentTrackedObjective> snapshot;

            lock (_lockObject)
            {
                if (_pendingObjectives.Count == 0)
                {
                    return;
                }

                snapshot = _pendingObjectives.Values.ToList();
                _pendingObjectives.Clear();
            }

            foreach (var objective in snapshot)
            {
                try
                {
                    if (!await UpdateObjectiveCountAsync(objective, cancellationToken).ConfigureAwait(false))
                    {
                        // Si falla (por ejemplo 429), volver a encolarlo para intentar más tarde
                        lock (_lockObject)
                        {
                            _pendingObjectives[objective.ObjectiveId] = objective;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[TarkovTracker] Error while flushing objective: " + ex.Message);
                    lock (_lockObject)
                    {
                        _pendingObjectives[objective.ObjectiveId] = objective;
                    }
                }
            }
        }

        public async Task UpdateTarkovTrackerAPI(bool force = false)
        {
            var settings = _settingsService.Settings;
            string apiKey = settings.TarkovTrackerApiKey;

            if (settings.UseTarkovTrackerApi && !string.Equals(apiKey, "APIKey") && !string.IsNullOrWhiteSpace(apiKey))
            {
                // If Outdated by 30 seconds
                if (force || ((DateTime.Now - LastUpdated).TotalSeconds >= 30))
                {
                    try
                    {
                        Debug.WriteLine("\n--> Updating TarkovTracker API...");

                        var client = _httpClientFactory.CreateClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                        var httpResponse = await client.GetAsync("https://tarkovtracker.io/api/v2/progress");
                        if (httpResponse.IsSuccessStatusCode)
                        {
                            string responseContent = await httpResponse.Content.ReadAsStringAsync();

                            List<CurrentTrackedObjective> pendingSnapshot;

                            lock (_lockObject)
                            {
                                TrackerData = JsonConvert.DeserializeObject<TarkovTrackerAPI.Root>(responseContent);
                                LastUpdated = DateTime.Now;
                                IsLoaded = true;
                                Debug.WriteLine("\n--> TarkovTracker API Updated!");

                                // Tomar un snapshot de los objetivos pendientes para re-aplicarlos después del GET
                                pendingSnapshot = _pendingObjectives.Values.ToList();
                            }

                            // Re-aplicar los cambios locales pendientes sobre los datos recién obtenidos
                            foreach (var obj in pendingSnapshot)
                            {
                                ApplyLocalObjectiveUpdate(obj);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("--> Error trying to update TarkovTracker API: " + ex.Message);
                    }
                }
                else
                {
                    Debug.WriteLine("--> No need to update TarkovTracker API!");
                }
            }
        }

        private async Task<bool> UpdateObjectiveCountAsync(CurrentTrackedObjective updatedObjective, CancellationToken cancellationToken)
        {
            var settings = _settingsService.Settings;
            string apiKey = settings.TarkovTrackerApiKey;

            if (!settings.UseTarkovTrackerApi || string.Equals(apiKey, "APIKey") || string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }

            // Si recientemente recibimos un 429, respetar un pequeño cooldown para no seguir martilleando la API
            if (DateTime.UtcNow - _lastTooManyRequests < TooManyRequestsCooldown)
            {
                Debug.WriteLine("[TarkovTracker] Skipping objective update due to recent 429 (cooldown in effect)");
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // According to TarkovTracker OpenAPI, both count and state are supported
                var state = updatedObjective.CurrentCount >= updatedObjective.RequiredCount ? "completed" : "uncompleted";
                var payload = new { count = updatedObjective.CurrentCount, state };
                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"https://tarkovtracker.io/api/v2/progress/task/objective/{updatedObjective.ObjectiveId}";
                Debug.WriteLine($"[TarkovTracker] Updating objective via {url} -> count={updatedObjective.CurrentCount}, state={state}");

                // OpenAPI specifies POST for this endpoint
                using var response = await client.PostAsync(url, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[TarkovTracker] Failed to update objective {updatedObjective.ObjectiveId}: {(int)response.StatusCode} {response.ReasonPhrase}");

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
                Debug.WriteLine("[TarkovTracker] Error while updating objective: " + ex.Message);
                return false;
            }
        }

        public CurrentTrackedObjective GetCurrentTrackedObjectiveForItem(Item item, Data tarkovData)
        {
            if (item == null || tarkovData == null || TrackerData == null || TrackerData.data == null)
            {
                return null;
            }

            var trackerData = TrackerData.data;

            // Prefer tasks over hideout, and among tasks prefer the ones with lower minPlayerLevel
            var usedInTasks = item.usedInTasks;
            if (usedInTasks == null || usedInTasks.Count == 0)
            {
                return null;
            }

            foreach (var task in usedInTasks.OrderBy(t => t.minPlayerLevel ?? int.MaxValue))
            {
                if (task.objectives == null)
                    continue;

                // Skip completed tasks entirely
                if (trackerData.tasksProgress != null && trackerData.tasksProgress.Exists(e => e.id == task.id && e.complete == true))
                    continue;

                foreach (var obj in task.objectives)
                {
                    if (obj.type == "findItem" && obj.foundInRaid == true && obj.items != null && obj.items.Any(i => i.id == item.id))
                    {
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

                        return new CurrentTrackedObjective
                        {
                            ObjectiveId = obj.id,
                            ItemId = item.id,
                            RequiredCount = required,
                            CurrentCount = current
                        };
                    }
                }
            }

            return null;
        }

        public TrackerUpdateResult TryIncrementCurrentObjectiveForCurrentItem(Item item, Data tarkovData)
        {
            var objective = GetCurrentTrackedObjectiveForItem(item, tarkovData);
            if (objective == null)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoObjectiveForItem);
            }

            if (objective.Remaining <= 0)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.AlreadyCompleted, objective);
            }

            var updated = new CurrentTrackedObjective
            {
                ObjectiveId = objective.ObjectiveId,
                ItemId = objective.ItemId,
                RequiredCount = objective.RequiredCount,
                CurrentCount = objective.CurrentCount + 1
            };

            Debug.WriteLine($"[TarkovTracker] Increment objective {updated.ObjectiveId} for item {updated.ItemId}: {objective.CurrentCount} -> {updated.CurrentCount}");
            return TrackerUpdateResult.Ok(updated);
        }

        public TrackerUpdateResult TryDecrementCurrentObjectiveForCurrentItem(Item item, Data tarkovData)
        {
            var objective = GetCurrentTrackedObjectiveForItem(item, tarkovData);
            if (objective == null)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoObjectiveForItem);
            }

            if (objective.CurrentCount <= 0)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoProgressToRemove, objective);
            }

            var updated = new CurrentTrackedObjective
            {
                ObjectiveId = objective.ObjectiveId,
                ItemId = objective.ItemId,
                RequiredCount = objective.RequiredCount,
                CurrentCount = objective.CurrentCount - 1
            };

            Debug.WriteLine($"[TarkovTracker] Decrement objective {updated.ObjectiveId} for item {updated.ItemId}: {objective.CurrentCount} -> {updated.CurrentCount}");
            return TrackerUpdateResult.Ok(updated);
        }

        public TrackerUpdateResult TryChangeCurrentObjectiveForCurrentItem(Item item, Data tarkovData, int delta)
        {
            if (delta == 0)
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.NoObjectiveForItem);
            }

            var objective = GetCurrentTrackedObjectiveForItem(item, tarkovData);
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
                    // Ya está completado, no podemos sumar más
                    return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.AlreadyCompleted, objective);
                }

                if (delta < 0 && objective.CurrentCount <= 0)
                {
                    // No hay progreso que quitar, pero tratar como no-op silencioso
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

            Debug.WriteLine($"[TarkovTracker] Change objective {updated.ObjectiveId} for item {updated.ItemId}: {objective.CurrentCount} -> {updated.CurrentCount} (delta={delta})");
            return TrackerUpdateResult.Ok(updated);
        }

        public TrackerUpdateResult ApplyLocalChangeForCurrentItem(Item item, Data tarkovData, int delta)
        {
            var validation = TryChangeCurrentObjectiveForCurrentItem(item, tarkovData, delta);
            if (!validation.Success)
            {
                return validation;
            }

            var updatedObjective = validation.Objective;

            // Actualizar el progreso localmente para que el overlay lo use inmediatamente
            ApplyLocalObjectiveUpdate(updatedObjective);

            // Encolar el objetivo para flush posterior a la API
            lock (_lockObject)
            {
                if (!string.IsNullOrEmpty(updatedObjective.ObjectiveId))
                {
                    _pendingObjectives[updatedObjective.ObjectiveId] = updatedObjective;
                }
            }

            return validation;
        }

        public async Task<TrackerUpdateResult> IncrementObjectiveAndSyncAsync(Item item, Data tarkovData, CancellationToken cancellationToken = default)
        {
            var validation = TryIncrementCurrentObjectiveForCurrentItem(item, tarkovData);
            if (!validation.Success)
            {
                return validation;
            }

            var updatedObjective = validation.Objective;
            if (!await UpdateObjectiveCountAsync(updatedObjective, cancellationToken).ConfigureAwait(false))
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.ApiError, updatedObjective);
            }

            // Actualizar TrackerData localmente para que el overlay se refresque sin un GET extra
            ApplyLocalObjectiveUpdate(updatedObjective);
            return validation;
        }

        public async Task<TrackerUpdateResult> DecrementObjectiveAndSyncAsync(Item item, Data tarkovData, CancellationToken cancellationToken = default)
        {
            var validation = TryDecrementCurrentObjectiveForCurrentItem(item, tarkovData);
            if (!validation.Success)
            {
                return validation;
            }

            var updatedObjective = validation.Objective;
            if (!await UpdateObjectiveCountAsync(updatedObjective, cancellationToken).ConfigureAwait(false))
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.ApiError, updatedObjective);
            }

            ApplyLocalObjectiveUpdate(updatedObjective);
            return validation;
        }

        public async Task<TrackerUpdateResult> ChangeObjectiveAndSyncAsync(Item item, Data tarkovData, int delta, CancellationToken cancellationToken = default)
        {
            var validation = TryChangeCurrentObjectiveForCurrentItem(item, tarkovData, delta);
            if (!validation.Success)
            {
                return validation;
            }

            var updatedObjective = validation.Objective;
            if (!await UpdateObjectiveCountAsync(updatedObjective, cancellationToken).ConfigureAwait(false))
            {
                return TrackerUpdateResult.Fail(TrackerUpdateFailureReason.ApiError, updatedObjective);
            }

            ApplyLocalObjectiveUpdate(updatedObjective);
            return validation;
        }
    }
}
