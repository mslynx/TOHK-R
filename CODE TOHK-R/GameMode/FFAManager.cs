﻿using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

internal static class FFAManager
{
    private static Dictionary<byte, long> FFAShieldedList = new();
    private static Dictionary<byte, long> FFAIncreasedSpeedList = new();
    private static Dictionary<byte, long> FFADecreasedSpeedList = new();
    public static Dictionary<byte, long> FFALowerVisionList = new();

    private static Dictionary<byte, float> originalSpeed = new();
    public static Dictionary<byte, int> KBScore = new();
    public static int RoundTime = new();

    //Options
    public static OptionItem FFA_GameTime;
    public static OptionItem FFA_KCD;
    public static OptionItem FFA_LowerVision;
    public static OptionItem FFA_IncreasedSpeed;
    public static OptionItem FFA_DecreasedSpeed;
    public static OptionItem FFA_ShieldDuration;
    public static OptionItem FFA_ModifiedVisionDuration;
    public static OptionItem FFA_ModifiedSpeedDuration;
    public static OptionItem FFA_DisableVentingWhenTwoPlayersAlive;
    public static OptionItem FFA_DisableVentingWhenKCDIsUp;
    public static OptionItem FFA_EnableRandomAbilities;
    public static OptionItem FFA_EnableRandomTwists;

    public static void SetupCustomOption()
    {
        FFA_GameTime = IntegerOptionItem.Create(67_223_001, "FFA_GameTime", new(30, 600, 10), 300, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);
        FFA_KCD = FloatOptionItem.Create(67_223_002, "FFA_KCD", new(1f, 60f, 1f), 10f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
        FFA_DisableVentingWhenTwoPlayersAlive = BooleanOptionItem.Create(67_223_003, "FFA_DisableVentingWhenTwoPlayersAlive", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));
        FFA_DisableVentingWhenKCDIsUp = BooleanOptionItem.Create(67_223_004, "FFA_DisableVentingWhenKCDIsUp", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));
        FFA_EnableRandomAbilities = BooleanOptionItem.Create(67_223_005, "FFA_EnableRandomAbilities", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));
        FFA_ShieldDuration = FloatOptionItem.Create(67_223_006, "FFA_ShieldDuration", new(1f, 70f, 1f), 7f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
        FFA_IncreasedSpeed = FloatOptionItem.Create(67_223_007, "FFA_IncreasedSpeed", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);
        FFA_DecreasedSpeed = FloatOptionItem.Create(67_223_008, "FFA_DecreasedSpeed", new(0.1f, 5f, 0.1f), 1f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);
        FFA_ModifiedSpeedDuration = FloatOptionItem.Create(67_223_009, "FFA_ModifiedSpeedDuration", new(1f, 60f, 1f), 10f, TabGroup.GameSettings, false).SetGameMode(CustomGameMode.FFA).SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
        FFA_LowerVision = FloatOptionItem.Create(67_223_010, "FFA_LowerVision", new(0f, 1f, 0.05f), 0.5f, TabGroup.GameSettings, false).SetGameMode(CustomGameMode.FFA).SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);
        FFA_ModifiedVisionDuration = FloatOptionItem.Create(67_223_011, "FFA_ModifiedVisionDuration", new(1f, 70f, 1f), 5f, TabGroup.GameSettings, false).SetGameMode(CustomGameMode.FFA).SetColor(new Color32(0, 255, 165, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
        FFA_EnableRandomTwists = BooleanOptionItem.Create(67_223_012, "FFA_EnableRandomTwists", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.FFA)
            .SetColor(new Color32(0, 255, 165, byte.MaxValue));
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.FFA) return;

        FFADecreasedSpeedList = new();
        FFAIncreasedSpeedList = new();
        FFALowerVisionList = new();
        FFAShieldedList = new();

        originalSpeed = new();
        KBScore = new();
        RoundTime = FFA_GameTime.GetInt() + 8;

        foreach (var pc in Main.AllAlivePlayerControls)
        {
            //PlayerHPMax.TryAdd(pc.PlayerId, KB_HPMax.GetFloat());
            //PlayerHP.TryAdd(pc.PlayerId, KB_HPMax.GetFloat());
            //PlayerHPReco.TryAdd(pc.PlayerId, KB_RecoverPerSecond.GetFloat());
            //PlayerATK.TryAdd(pc.PlayerId, KB_ATK.GetFloat());
            //PlayerDF.TryAdd(pc.PlayerId, 0f);

            KBScore.TryAdd(pc.PlayerId, 0);
        }
    }
    private static void SendRPCSyncFFAPlayer(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncFFAPlayer, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(KBScore[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPCSyncFFAPlayer(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        KBScore[PlayerId] = reader.ReadInt32();
    }
    public static void SendRPCSyncNameNotify(PlayerControl pc)
    {
        if (pc.AmOwner || !pc.IsModClient()) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncFFANameNotify, SendOption.Reliable, pc.GetClientId());
        if (NameNotify.ContainsKey(pc.PlayerId))
            writer.Write(NameNotify[pc.PlayerId].Item1);
        else writer.Write(string.Empty);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPCSyncNameNotify(MessageReader reader)
    {
        var name = reader.ReadString();
        NameNotify.Remove(PlayerControl.LocalPlayer.PlayerId);
        if (name != null && name != string.Empty)
            NameNotify.Add(PlayerControl.LocalPlayer.PlayerId, (name, 0));
    }
    public static Dictionary<byte, (string, long)> NameNotify = new();
    public static void GetNameNotify(PlayerControl player, ref string name)
    {
        if (Options.CurrentGameMode != CustomGameMode.FFA || player == null) return;
        if (NameNotify.ContainsKey(player.PlayerId))
        {
            name = NameNotify[player.PlayerId].Item1;
            return;
        }
    }
    public static string GetDisplayScore(byte playerId)
    {
        int rank = GetRankOfScore(playerId);
        string score = KBScore.TryGetValue(playerId, out var s) ? $"{s}" : "Invalid";
        string text = string.Format(GetString("KBDisplayScore"), rank.ToString(), score);
        Color color = Utils.GetRoleColor(CustomRoles.Killer);
        return Utils.ColorString(color, text);
    }
    public static int GetRankOfScore(byte playerId)
    {
        try
        {
            int ms = KBScore[playerId];
            int rank = 1 + KBScore.Values.Where(x => x > ms).Count();
            rank += KBScore.Where(x => x.Value == ms).ToList().IndexOf(new(playerId, ms));
            return rank;
        }
        catch
        {
            return Main.AllPlayerControls.Count();
        }
    }
    public static string GetHudText()
    {
        return string.Format(GetString("KBTimeRemain"), RoundTime.ToString());
    }
    public static void OnPlayerAttack(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || Options.CurrentGameMode != CustomGameMode.FFA) return;
        if (target.inVent) return;
        var totalalive = Main.AllAlivePlayerControls.Count();
        if (FFAShieldedList.ContainsKey(target.PlayerId))
        {
            killer.Notify(GetString("FFATargetIsShielded"));
            return;
        }

        OnPlayerKill(killer);

        SendRPCSyncFFAPlayer(target.PlayerId);

        if (totalalive <= 3)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(a => a.PlayerId != killer.PlayerId && a.PlayerId != target.PlayerId && a.IsAlive()))
            {
                TargetArrow.Add(killer.PlayerId, pc.PlayerId);
                TargetArrow.Add(pc.PlayerId, killer.PlayerId);
            }
        }

        if (FFA_EnableRandomAbilities.GetBool())
        {
            byte EffectType;
            if (Main.NormalOptions.MapId != 4) EffectType = (byte)HashRandom.Next(0, 10);
            else EffectType = (byte)HashRandom.Next(4, 10);
            if (EffectType <= 7) // Buff
            {
                byte EffectID = (byte)HashRandom.Next(0, 3);
                if (Main.NormalOptions.MapId == 4) EffectID = 2;
                switch (EffectID)
                {
                    case 0:
                        FFAShieldedList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                        killer.Notify(GetString("FFA-Event-GetShield"), FFA_ShieldDuration.GetFloat());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = FFA_KCD.GetFloat();
                        break;
                    case 1:
                        if (FFAIncreasedSpeedList.ContainsKey(killer.PlayerId))
                        {
                            FFAIncreasedSpeedList.Remove(killer.PlayerId);
                            FFAIncreasedSpeedList.Add(killer.PlayerId, Utils.GetTimeStamp());
                        }
                        else
                        {
                            FFAIncreasedSpeedList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                            originalSpeed.TryAdd(killer.PlayerId, Main.AllPlayerSpeed[killer.PlayerId]);
                            Main.AllPlayerSpeed[killer.PlayerId] = FFA_IncreasedSpeed.GetFloat();
                        }
                        killer.Notify(GetString("FFA-Event-GetIncreasedSpeed"), FFA_ModifiedSpeedDuration.GetFloat());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = FFA_KCD.GetFloat();
                        break;
                    case 2:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = System.Math.Clamp(FFA_KCD.GetFloat() - 3f, 1f, 60f);
                        killer.Notify(GetString("FFA-Event-GetLowKCD"));
                        break;
                    default:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = FFA_KCD.GetFloat();
                        break;
                }
            }
            else if (EffectType == 8) // De-Buff
            {
                byte EffectID = (byte)HashRandom.Next(0, 3);
                if (Main.NormalOptions.MapId == 4) EffectID = 1;
                switch (EffectID)
                {
                    case 0:
                        if (FFADecreasedSpeedList.ContainsKey(killer.PlayerId))
                        {
                            FFADecreasedSpeedList.Remove(killer.PlayerId);
                            FFADecreasedSpeedList.Add(killer.PlayerId, Utils.GetTimeStamp());
                        }
                        else
                        {
                            FFADecreasedSpeedList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                            originalSpeed.TryAdd(killer.PlayerId, Main.AllPlayerSpeed[killer.PlayerId]);
                            Main.AllPlayerSpeed[killer.PlayerId] = FFA_DecreasedSpeed.GetFloat();
                        }
                        killer.Notify(GetString("FFA-Event-GetDecreasedSpeed"), FFA_ModifiedSpeedDuration.GetFloat());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = FFA_KCD.GetFloat();
                        break;
                    case 1:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = System.Math.Clamp(FFA_KCD.GetFloat() + 3f, 1f, 60f);
                        killer.Notify(GetString("FFA-Event-GetHighKCD"));
                        break;
                    case 2:
                        FFALowerVisionList.TryAdd(killer.PlayerId, Utils.GetTimeStamp());
                        Main.AllPlayerKillCooldown[killer.PlayerId] = FFA_KCD.GetFloat();
                        killer.Notify(GetString("FFA-Event-GetLowVision"));
                        break;
                    default:
                        Main.AllPlayerKillCooldown[killer.PlayerId] = FFA_KCD.GetFloat();
                        break;
                }
            }
            else // Mixed
            {
                var rd = IRandom.Instance;
                var vents = Object.FindObjectsOfType<Vent>();
                var vent = vents[rd.Next(0, vents.Count)];
                _ = new LateTask(() => { Utils.TP(killer.NetTransform, new Vector2(vent.transform.position.x, vent.transform.position.y)); }, 0.5f);
                killer.Notify(GetString("FFA-Event-GetTP"));
                Main.AllPlayerKillCooldown[killer.PlayerId] = FFA_KCD.GetFloat();
            }

            killer.SyncSettings();
        }

        killer.RpcMurderPlayerV3(target);
    }

    public static void OnPlayerKill(PlayerControl killer)
    {
        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
            PlayerControl.LocalPlayer.KillFlash();

        KBScore[killer.PlayerId]++;
    }

    public static string GetPlayerArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (GameStates.IsMeeting) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        if (Main.AllAlivePlayerControls.Count() != 2) return string.Empty;

        string arrows = string.Empty;
        PlayerControl otherPlayer = null;
        foreach (var pc in Main.AllAlivePlayerControls.Where(pc => pc.IsAlive() && pc.PlayerId != seer.PlayerId))
        {
            otherPlayer = pc;
            break;
        }
        if (otherPlayer == null) return string.Empty;

        var arrow = TargetArrow.GetArrows(seer, otherPlayer.PlayerId);
        arrows += Utils.ColorString(Utils.GetRoleColor(CustomRoles.Killer), arrow);

        return arrows;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        private static long LastFixedUpdate = new();
        public static void Postfix(PlayerControl __instance)
        {
            if (!GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.FFA) return;

            if (AmongUsClient.Instance.AmHost)
            {
                if (LastFixedUpdate == Utils.GetTimeStamp()) return;
                LastFixedUpdate = Utils.GetTimeStamp();

                RoundTime--;

                foreach (var pc in Main.AllPlayerControls)
                {
                    if (NameNotify.ContainsKey(pc.PlayerId) && NameNotify[pc.PlayerId].Item2 < Utils.GetTimeStamp())
                    {
                        NameNotify.Remove(pc.PlayerId);
                        SendRPCSyncNameNotify(pc);
                        Utils.NotifyRoles(SpecifySeer: pc);
                    }
                }

                byte FFAdoTPdecider = (byte)HashRandom.Next(0, 100);
                bool FFAdoTP = false;
                if (FFAdoTPdecider == 0) FFAdoTP = true;

                if (FFA_EnableRandomTwists.GetBool() && FFAdoTP)
                {
                    List<byte> changePositionPlayers = new();

                    var rd = IRandom.Instance;
                    foreach (var pc in Main.AllAlivePlayerControls)
                    {
                        if (changePositionPlayers.Contains(pc.PlayerId) || !pc.IsAlive() || pc.onLadder || pc.inVent) continue;

                        var filtered = Main.AllAlivePlayerControls.Where(a =>
                            pc.IsAlive() && !pc.inVent && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToList();
                        if (!filtered.Any()) break;

                        PlayerControl target = filtered[rd.Next(0, filtered.Count)];

                        if (pc.inVent || target.inVent) continue;

                        changePositionPlayers.Add(target.PlayerId);
                        changePositionPlayers.Add(pc.PlayerId);

                        pc.RPCPlayCustomSound("Teleport");

                        var originPs = target.GetTruePosition();
                        Utils.TP(target.NetTransform, pc.GetTruePosition());
                        Utils.TP(pc.NetTransform, originPs);

                        target.Notify(Utils.ColorString(new Color32(0, 255, 165, byte.MaxValue), string.Format(GetString("FFA-Event-RandomTP"), pc.GetRealName())));
                        pc.Notify(Utils.ColorString(new Color32(0, 255, 165, byte.MaxValue), string.Format(GetString("FFA-Event-RandomTP"), target.GetRealName())));
                    }

                    changePositionPlayers.Clear();
                }

                if (Main.NormalOptions.MapId == 4) return;

                if (FFADecreasedSpeedList.TryGetValue(__instance.PlayerId, out var dstime) && dstime + FFA_ModifiedSpeedDuration.GetInt() < Utils.GetTimeStamp())
                {
                    FFADecreasedSpeedList.Remove(__instance.PlayerId);
                    Main.AllPlayerSpeed[__instance.PlayerId] = originalSpeed[__instance.PlayerId];
                    originalSpeed.Remove(__instance.PlayerId);
                    __instance.SyncSettings();
                }
                if (FFAIncreasedSpeedList.TryGetValue(__instance.PlayerId, out var istime) && istime + FFA_ModifiedSpeedDuration.GetInt() < Utils.GetTimeStamp())
                {
                    FFAIncreasedSpeedList.Remove(__instance.PlayerId);
                    Main.AllPlayerSpeed[__instance.PlayerId] = originalSpeed[__instance.PlayerId];
                    originalSpeed.Remove(__instance.PlayerId);
                    __instance.SyncSettings();
                }
                if (FFALowerVisionList.TryGetValue(__instance.PlayerId, out var lvtime) && lvtime + FFA_ModifiedSpeedDuration.GetInt() < Utils.GetTimeStamp())
                {
                    FFALowerVisionList.Remove(__instance.PlayerId);
                    __instance.SyncSettings();
                }
                if (FFAShieldedList.TryGetValue(__instance.PlayerId, out var stime) && stime + FFA_ShieldDuration.GetInt() < Utils.GetTimeStamp())
                {
                    FFAShieldedList.Remove(__instance.PlayerId);
                }
            }
        }
    }
}