using Onyx.Core.Audio;
using Onyx.Core.CrashSite;
using Onyx.Game.Audio;
using Onyx.Game.CrashSite;
using Onyx.Game.DevTools;
using Onyx.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Taste E halten: kaputte Station reparieren oder kampfunfähigen Mitspieler wiederbeleben.
    /// Der Besitzer sucht das Ziel und meldet Beginn/Ende per RPC; gerechnet wird beim Host.
    /// </summary>
    public sealed class PlayerInteraction : NetworkBehaviour
    {
        /// <summary>Reichweite für Interaktionen in Kacheln (von den Füßen aus).</summary>
        public const float Range = 1.6f;

        /// <summary>Die eigene Figur auf diesem Rechner.</summary>
        public static PlayerInteraction Local { get; private set; }

        private PlayerVitals vitals;

        // Was der Besitzer gerade tut (für Anzeige und um Ende zu melden)
        private int workingStation = -1;
        private float ratchetCooldown;
        private PlayerVitals revivingTarget;

        /// <summary>Text für das HUD, z. B. „[E] halten: Reaktor reparieren“. null = nichts in Reichweite.</summary>
        public string Prompt { get; private set; }

        /// <summary>Fortschritt der aktuellen Handlung (0–1) oder -1, wenn keiner angezeigt werden soll.</summary>
        public float PromptProgress { get; private set; } = -1f;

        /// <summary>Gesperrte Handlung (z. B. fehlender Reaktor) – das HUD zeigt sie rot.</summary>
        public bool PromptBlocked { get; private set; }

        public bool IsBusy => workingStation >= 0 || revivingTarget != null;

        private void Awake()
        {
            vitals = GetComponent<PlayerVitals>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Local = this;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this)
            {
                Local = null;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            var site = CrashSiteNet.ActiveSite;
            bool canAct = site != null && (vitals == null || !vitals.IsDowned) && !GameplayInput.Blocked;
            Keyboard keyboard = Keyboard.current;
            bool holding = canAct && keyboard != null && keyboard.eKey.isPressed;
            Vector3 p = transform.position;

            // 1. Kampfunfähiger Mitspieler in der Nähe hat Vorrang.
            PlayerVitals downedMate = canAct ? FindDownedMate(p) : null;
            RepairPoint station = canAct && downedMate == null ? site.NearestOpen(p.x, p.y, Range) : null;

            if (downedMate != null)
            {
                Prompt = "[E] halten: Mitspieler wiederbeleben";
                PromptProgress = downedMate.ReviveProgress > 0f ? downedMate.ReviveProgress : -1f;
                PromptBlocked = false;
                StopWork();
                if (holding && revivingTarget != downedMate)
                {
                    StopRevive();
                    revivingTarget = downedMate;
                    ReviveRpc(downedMate.NetworkObjectId, true);
                }
                else if (!holding)
                {
                    StopRevive();
                }

                return;
            }

            StopRevive();
            if (station == null)
            {
                Prompt = null;
                PromptProgress = -1f;
                PromptBlocked = false;
                StopWork();
                return;
            }

            var job = site.Job(station.Index);
            JoinResult check = site.CanWorkOn(station.Index);
            PromptBlocked = check != JoinResult.Ok;
            Prompt = PromptBlocked ? CrashSiteState.Describe(check, station, site) : $"[E] halten: {station.Name} reparieren";
            PromptProgress = job.Progress > 0f ? job.Ratio : -1f;

            if (holding && !PromptBlocked)
            {
                if (workingStation != station.Index)
                {
                    workingStation = station.Index;
                    RepairRpc(station.Index, true);
                }

                ratchetCooldown -= Time.deltaTime;
                if (ratchetCooldown <= 0f)
                {
                    GameAudio.Play(Sfx.Ratchet, 0.4f, Random.Range(0.9f, 1.1f));
                    ratchetCooldown = 0.34f;
                }
            }
            else
            {
                StopWork();
            }
        }

        private PlayerVitals FindDownedMate(Vector3 p)
        {
            PlayerVitals best = null;
            float bestDistance = Range;
            foreach (var other in PlayerVitals.All)
            {
                if (other == vitals || other == null || !other.IsDowned) continue;
                float d = Vector2.Distance(p, other.transform.position);
                if (d <= bestDistance)
                {
                    best = other;
                    bestDistance = d;
                }
            }

            return best;
        }

        private void StopWork()
        {
            if (workingStation >= 0)
            {
                workingStation = -1;
                RepairRpc(-1, false);
            }
        }

        private void StopRevive()
        {
            if (revivingTarget != null)
            {
                ulong id = revivingTarget.NetworkObjectId;
                revivingTarget = null;
                ReviveRpc(id, false);
            }
        }

        // ------------------------------------------------------------------ Host

        [Rpc(SendTo.Server)]
        private void RepairRpc(int stationIndex, bool start, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || CrashSiteNet.Instance == null)
            {
                return;
            }

            if (start && (vitals == null || !vitals.IsDowned))
            {
                CrashSiteNet.Instance.ServerJoin(stationIndex, OwnerClientId);
            }
            else
            {
                CrashSiteNet.Instance.ServerLeave(OwnerClientId);
            }
        }

        [Rpc(SendTo.Server)]
        private void ReviveRpc(ulong targetObjectId, bool start, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetObjectId, out var target))
            {
                return;
            }

            var targetVitals = target.GetComponent<PlayerVitals>();
            if (targetVitals == null)
            {
                return;
            }

            if (start && Vector2.Distance(transform.position, target.transform.position) <= Range + 0.5f)
            {
                targetVitals.ServerAddReviver(OwnerClientId);
            }
            else
            {
                targetVitals.ServerRemoveReviver(OwnerClientId);
            }
        }
    }
}
