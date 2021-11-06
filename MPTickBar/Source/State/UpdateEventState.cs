﻿using ImGuiNET;
using System;
using System.Runtime.InteropServices;

namespace MPTickBar
{
    public class UpdateEventState
    {
        public PlayerState PlayerState { get; set; }

        private UpdateEventData<double> Time { get; set; } = new();

        private UpdateEventData<uint> MP { get; set; } = new();

        private UpdateEventData<ushort> Territory { get; set; } = new();

        private UpdateEventData<bool> IsInCombat { get; set; } = new();

        private UpdateEventData<bool> IsDead { get; set; } = new();

        private UpdateEventData<bool> IsManafontOnCooldown { get; set; } = new();

        private UpdateEventData<double> Progress { get; set; } = new();

        private double MPRegenSkipTime { get; set; }

        private bool IsProgressEnabled { get; set; }

        private class UpdateEventData<T> where T : struct
        {
            public T Current { get; set; }

            public T Last { get; private set; }

            public void SaveData()
            {
                this.Last = this.Current;
            }
        }

        public void Login(object sender, EventArgs e)
        {
            this.ResetDisableProgress();
        }

        private static int GetData(IntPtr dataPtr, int offset, int size)
        {
            var bytes = new byte[4];
            Marshal.Copy(dataPtr + offset, bytes, 0, size);
            return BitConverter.ToInt32(bytes);
        }

        private static int GetHP(IntPtr dataPtr)
        {
            return UpdateEventState.GetData(dataPtr, 0, 3);
        }

        private static int GetMP(IntPtr dataPtr)
        {
            return UpdateEventState.GetData(dataPtr, 4, 2);
        }

        private static int GetTickIncrement(IntPtr dataPtr)
        {
            return UpdateEventState.GetData(dataPtr, 6, 2);
        }

        public void _DEBUG_LOG_DATA_(IntPtr dataPtr, uint targetActorId)
        {
            var bytes = new byte[384];
            Marshal.Copy(dataPtr, bytes, 0, bytes.Length);
            Dalamud.Logging.PluginLog.Information($"{UpdateEventState.GetHP(dataPtr):000000}|{UpdateEventState.GetMP(dataPtr):00000}|{UpdateEventState.GetTickIncrement(dataPtr):00000}|{targetActorId:0000000000} ({((targetActorId == this.PlayerState.Id) ? "X" : " ")}): {BitConverter.ToString(bytes)}");
        }

        public void NetworkMessage(IntPtr dataPtr, uint targetActorId)
        {
            var isProgressStopped = (this.Progress.Current == 0) && (this.Progress.Last == 0);
            var idCheck = (this.PlayerState.Id == targetActorId);
            if (!this.IsDead.Current && !this.IsInCombat.Current && isProgressStopped && idCheck && (UpdateEventState.GetHP(dataPtr) == this.PlayerState.HP) && (UpdateEventState.GetMP(dataPtr) == this.PlayerState.MPMax))
                this.RestartProgress();
        }

        private void ResetDisableProgress()
        {
            this.Progress.Current = 0;
            this.IsProgressEnabled = false;
        }

        private void RestartProgress()
        {
            if (this.Progress.Current > 0.5)
                this.Progress.Current = 0;

            this.IsProgressEnabled = true;
        }

        private void AddMPRegenSkipTime()
        {
            this.MPRegenSkipTime += 3.5;
        }

        private bool OnMPRegenLucidDreaming()
        {
            if (this.PlayerState.IsLucidDreamingActivated)
            {
                var lucidDreamingPotency = 50;
                var mpReturned = lucidDreamingPotency * 10000 / 1000;
                var mpRecovered = this.MP.Current - this.MP.Last;
                var iSrecoveringMPToFull = (this.MP.Current == this.PlayerState.MPMax) && (this.MP.Last > (this.PlayerState.MPMax - mpReturned));

                return (mpRecovered > 0) && (mpRecovered == mpReturned || iSrecoveringMPToFull);
            }
            return false;
        }

        private void OnManafontUsage()
        {
            if (!this.IsManafontOnCooldown.Last && this.IsManafontOnCooldown.Current)
                this.AddMPRegenSkipTime();
        }

        private void OnReviveInCombat()
        {
            if (this.IsDead.Last && !this.IsDead.Current && this.IsInCombat.Current)
                this.AddMPRegenSkipTime();
        }

        private void OnMPRegen(bool onMPRegenLucidDreaming, double interval)
        {
            this.MPRegenSkipTime -= interval;
            if (this.MPRegenSkipTime < 0)
                this.MPRegenSkipTime = 0;

            var mpReset = (this.MP.Last == 0) && (this.MP.Current == this.PlayerState.MPMax);
            var onMPRegen = (this.MP.Last < this.MP.Current) && !mpReset && !onMPRegenLucidDreaming && (this.MPRegenSkipTime == 0);
            if (onMPRegen)
                this.RestartProgress();
        }

        private void OnZoneChange()
        {
            if (this.Territory.Last != this.Territory.Current)
                this.ResetDisableProgress();
        }

        private void OnLeaveCombat()
        {
            if (this.IsInCombat.Last && !this.IsInCombat.Current)
                this.ResetDisableProgress();
        }

        private void OnDeath()
        {
            if (!this.IsDead.Last && this.IsDead.Current)
                this.ResetDisableProgress();
        }

        private void ProgressUpdate(double interval)
        {
            if (this.IsProgressEnabled)
            {
                var mpTickSecondsTotal = 3.0;
                this.Progress.Current += interval;
                if (this.Progress.Current >= mpTickSecondsTotal)
                    this.Progress.Current -= mpTickSecondsTotal;
            }
        }

        public double Update()
        {
            this.Time.Current = ImGui.GetTime();
            this.MP.Current = this.PlayerState.MP;
            this.Territory.Current = this.PlayerState.TerritoryType;
            this.IsInCombat.Current = this.PlayerState.IsInCombat;
            this.IsDead.Current = this.PlayerState.IsDead;
            this.IsManafontOnCooldown.Current = this.PlayerState.IsManafontOnCooldown();

            var interval = (this.Time.Current - this.Time.Last);

            var onMPRegenLucidDreaming = this.OnMPRegenLucidDreaming();
            this.OnManafontUsage();
            this.OnReviveInCombat();
            this.OnMPRegen(onMPRegenLucidDreaming, interval);
            this.OnZoneChange();
            this.OnLeaveCombat();
            this.OnDeath();

            this.ProgressUpdate(interval);

            this.Time.SaveData();
            this.MP.SaveData();
            this.Territory.SaveData();
            this.IsInCombat.SaveData();
            this.IsDead.SaveData();
            this.IsManafontOnCooldown.SaveData();
            this.Progress.SaveData();

            return this.Progress.Current;
        }
    }
}