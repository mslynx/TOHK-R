using HarmonyLib;
using Hazel;
using System;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using static TOHE.Translator;

namespace TOHE;

/*
 * HUGE THANKS TO
 * ImaMapleTree / 단풍잎 / Tealeaf
 * FOR THE CODE
 */

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
class LocalPetPatch
{
    private static System.Collections.Generic.Dictionary<byte, long> LastProcess = new();
    public static bool Prefix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return true;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return true;
        if (GameStates.IsLobby) return true;

        if (__instance.petting) return true;
        __instance.petting = true;

        if (!LastProcess.ContainsKey(__instance.PlayerId)) LastProcess.TryAdd(__instance.PlayerId, Utils.GetTimeStamp() - 2);
        if (LastProcess[__instance.PlayerId] + 1 >= Utils.GetTimeStamp()) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, 51);

        LastProcess[__instance.PlayerId] = Utils.GetTimeStamp();
        return false;
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return;
        __instance.petting = false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
class ExternalRpcPetPatch
{
    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId)
    {
        if (!Options.UsePets.GetBool()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        var rpcType = callId == 51 ? RpcCalls.Pet : (RpcCalls)callId;
        if (rpcType != RpcCalls.Pet) return;

        PlayerControl pc = __instance.myPlayer;

        //if (callId == 51 && pc.GetCustomRole().PetActivatedAbility() && GameStates.IsInGame) __instance.CancelPet();
        if (callId != 51)
        {
            if (AmongUsClient.Instance.AmHost && pc.GetCustomRole().PetActivatedAbility() && GameStates.IsInGame)
                __instance.CancelPet();
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                AmongUsClient.Instance.FinishRpcImmediately(AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, 50, SendOption.None, player.GetClientId()));
        }

        Logger.Info($"Player {pc.GetNameWithRole()} has Pet", "RPCDEBUG");

        OnPetUse(pc);
    }
    public static void OnPetUse(PlayerControl pc)
    {
        if (pc == null) return;

        switch (pc.GetCustomRole())
        {
            // Crewmates

            case CustomRoles.Doormaster:
                if (Main.DoormasterCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Doormaster.OnEnterVent(pc);
                pc.RpcResetAbilityCooldown();
                break;
            case CustomRoles.Tether:
                if (Main.TetherCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Tether.OnEnterVent(pc, 0, true);
                pc.RpcResetAbilityCooldown();
                break;
            case CustomRoles.Mayor:
                if (Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.MayorNumOfUseButton.GetInt() && !Main.MayorCD.ContainsKey(pc.PlayerId))
                {
                    pc?.ReportDeadBody(null);
                }
                break;
            case CustomRoles.Paranoia:
                if (Main.ParaUsedButtonCount.TryGetValue(pc.PlayerId, out var count2) && count2 < Options.ParanoiaNumOfUseButton.GetInt() && !Main.ParanoiaCD.ContainsKey(pc.PlayerId))
                {
                    Main.ParaUsedButtonCount[pc.PlayerId] += 1;
                    if (AmongUsClient.Instance.AmHost)
                    {
                        _ = new LateTask(() =>
                        {
                            Utils.SendMessage(GetString("SkillUsedLeft") + (Options.ParanoiaNumOfUseButton.GetInt() - Main.ParaUsedButtonCount[pc.PlayerId]).ToString(), pc.PlayerId);
                        }, 4.0f, "Skill Remain Message");
                    }

                    pc?.NoCheckStartMeeting(pc?.Data);
                }
                break;
            case CustomRoles.Veteran:
                if (Main.VeteranInProtect.ContainsKey(pc.PlayerId)) break;
                if (Main.VeteranNumOfUsed[pc.PlayerId] >= 1)
                {
                    if (Main.VeteranCD.ContainsKey(pc.PlayerId))
                    {
                        //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                        break;
                    }
                    Main.VeteranInProtect.Remove(pc.PlayerId);
                    Main.VeteranInProtect.Add(pc.PlayerId, Utils.GetTimeStamp(DateTime.Now));
                    Main.VeteranNumOfUsed[pc.PlayerId] -= 1;
                    //pc.RpcGuardAndKill(pc);
                    pc.RPCPlayCustomSound("Gunload");
                    pc.Notify(GetString("VeteranOnGuard"), Options.VeteranSkillDuration.GetFloat());
                    Main.VeteranCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                    pc.RpcResetAbilityCooldown();
                    pc.MarkDirtySettings();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.Grenadier:
                if (Main.GrenadierBlinding.ContainsKey(pc.PlayerId) || Main.MadGrenadierBlinding.ContainsKey(pc.PlayerId)) break;
                if (Main.GrenadierNumOfUsed[pc.PlayerId] >= 1)
                {
                    if (Main.GrenadierCD.ContainsKey(pc.PlayerId))
                    {
                        //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                        break;
                    }
                    if (pc.Is(CustomRoles.Madmate))
                    {
                        Main.MadGrenadierBlinding.Remove(pc.PlayerId);
                        Main.MadGrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => !x.GetCustomRole().IsImpostorTeam() && !x.Is(CustomRoles.Madmate)).Do(x => x.RPCPlayCustomSound("FlashBang"));
                    }
                    else
                    {
                        Main.GrenadierBlinding.Remove(pc.PlayerId);
                        Main.GrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => x.GetCustomRole().IsImpostor() || (x.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool())).Do(x => x.RPCPlayCustomSound("FlashBang"));
                    }
                    //pc.RpcGuardAndKill(pc);
                    pc.RPCPlayCustomSound("FlashBang");
                    pc.Notify(GetString("GrenadierSkillInUse"), Options.GrenadierSkillDuration.GetFloat());
                    Main.GrenadierCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                    pc.RpcResetAbilityCooldown();
                    Main.GrenadierNumOfUsed[pc.PlayerId] -= 1;
                    Utils.MarkEveryoneDirtySettingsV3();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.Lighter:
                if (Main.Lighter.ContainsKey(pc.PlayerId)) break;
                if (Main.LighterNumOfUsed[pc.PlayerId] >= 1)
                {
                    if (Main.LighterCD.ContainsKey(pc.PlayerId))
                    {
                        //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                        break;
                    }
                    Main.Lighter.Remove(pc.PlayerId);
                    Main.Lighter.Add(pc.PlayerId, Utils.GetTimeStamp());
                    pc.Notify(GetString("LighterSkillInUse"), Options.LighterSkillDuration.GetFloat());
                    Main.LighterCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                    pc.RpcResetAbilityCooldown();
                    Main.LighterNumOfUsed[pc.PlayerId] -= 1;
                    pc.MarkDirtySettings();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.SecurityGuard:
                if (Main.BlockSabo.ContainsKey(pc.PlayerId)) break;
                if (Main.SecurityGuardNumOfUsed[pc.PlayerId] >= 1)
                {
                    if (Main.SecurityGuardCD.ContainsKey(pc.PlayerId))
                    {
                        //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                        break;
                    }
                    Main.BlockSabo.Remove(pc.PlayerId);
                    Main.BlockSabo.Add(pc.PlayerId, Utils.GetTimeStamp());
                    pc.Notify(GetString("SecurityGuardSkillInUse"), Options.SecurityGuardSkillDuration.GetFloat());
                    Main.SecurityGuardCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                    pc.RpcResetAbilityCooldown();
                    Main.SecurityGuardNumOfUsed[pc.PlayerId] -= 1;
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.DovesOfNeace:
                if (Main.DovesOfNeaceNumOfUsed[pc.PlayerId] < 1)
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                else if (Main.DovesOfNeaceCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                }
                else
                {
                    Main.DovesOfNeaceNumOfUsed[pc.PlayerId] -= 1;
                    //pc.RpcGuardAndKill(pc);
                    Main.AllAlivePlayerControls.Where(x =>
                    pc.Is(CustomRoles.Madmate) ?
                    (x.CanUseKillButton() && x.GetCustomRole().IsCrewmate()) :
                    x.CanUseKillButton()
                    ).Do(x =>
                    {
                        x.RPCPlayCustomSound("Dove");
                        x.ResetKillCooldown();
                        x.SetKillCooldown();
                        if (x.Is(CustomRoles.SerialKiller))
                        { SerialKiller.OnReportDeadBody(); }
                        x.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DovesOfNeace), GetString("DovesOfNeaceSkillNotify")));
                    });
                    pc.RPCPlayCustomSound("Dove");
                    pc.Notify(string.Format(GetString("DovesOfNeaceOnGuard"), Main.DovesOfNeaceNumOfUsed[pc.PlayerId]));
                    Main.DovesOfNeaceCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                    pc.RpcResetAbilityCooldown();
                }
                break;
            case CustomRoles.Alchemist:
                Alchemist.OnEnterVent(pc, 0, true);
                pc.RpcResetAbilityCooldown();
                break;
            case CustomRoles.TimeMaster:
                if (Main.TimeMasterInProtect.ContainsKey(pc.PlayerId)) break;
                if (Main.TimeMasterNumOfUsed[pc.PlayerId] >= 1)
                {
                    if (Main.TimeMasterCD.ContainsKey(pc.PlayerId))
                    {
                        //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                        break;
                    }
                    Main.TimeMasterNumOfUsed[pc.PlayerId] -= 1;
                    Main.TimeMasterInProtect.Remove(pc.PlayerId);
                    Main.TimeMasterInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
                    //if (!pc.IsModClient()) pc.RpcGuardAndKill(pc);
                    pc.Notify(GetString("TimeMasterOnGuard"), Options.TimeMasterSkillDuration.GetFloat());
                    Main.TimeMasterCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                    pc.RpcResetAbilityCooldown();
                    foreach (var player in Main.AllPlayerControls)
                    {
                        if (Main.TimeMasterBackTrack.ContainsKey(player.PlayerId))
                        {
                            var position = Main.TimeMasterBackTrack[player.PlayerId];
                            Utils.TP(player.NetTransform, position);
                            if (pc != player)
                                player?.MyPhysics?.RpcBootFromVent(player.PlayerId);
                            Main.TimeMasterBackTrack.Remove(player.PlayerId);
                        }
                        else
                        {
                            Main.TimeMasterBackTrack.Add(player.PlayerId, player.GetTruePosition());
                        }
                    }
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.NiceHacker:
                NiceHacker.OnEnterVent(pc);
                pc.RpcResetAbilityCooldown();
                break;

            // Impostors

            case CustomRoles.Sniper:
                if (Main.SniperCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                if (Sniper.IsAim[pc.PlayerId]) Main.SniperCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                Sniper.OnShapeshift(pc, !Sniper.IsAim[pc.PlayerId]);
                break;
            case CustomRoles.Warlock:
                if (Main.CursedPlayers[pc.PlayerId] != null)//呪われた人がいるか確認
                {
                    if (!Main.CursedPlayers[pc.PlayerId].Data.IsDead)
                    {
                        var cp = Main.CursedPlayers[pc.PlayerId];
                        UnityEngine.Vector2 cppos = cp.transform.position;
                        System.Collections.Generic.Dictionary<PlayerControl, float> cpdistance = new();
                        float dis;
                        foreach (PlayerControl p in Main.AllAlivePlayerControls)
                        {
                            if (p.PlayerId == cp.PlayerId) continue;
                            if (!Options.WarlockCanKillSelf.GetBool() && p.PlayerId == pc.PlayerId) continue;
                            if (!Options.WarlockCanKillAllies.GetBool() && p.GetCustomRole().IsImpostor()) continue;
                            if (p.Is(CustomRoles.Pestilence)) continue;
                            if (Pelican.IsEaten(p.PlayerId) || Medic.ProtectList.Contains(p.PlayerId)) continue;
                            dis = UnityEngine.Vector2.Distance(cppos, p.transform.position);
                            cpdistance.Add(p, dis);
                            Logger.Info($"{p?.Data?.PlayerName}の位置{dis}", "Warlock");
                        }
                        if (cpdistance.Any())
                        {
                            var min = cpdistance.OrderBy(c => c.Value).FirstOrDefault();
                            PlayerControl targetw = min.Key;
                            if (cp.RpcCheckAndMurder(targetw, true))
                            {
                                targetw.SetRealKiller(pc);
                                Logger.Info($"{targetw.GetNameWithRole()}was killed", "Warlock");
                                cp.RpcMurderPlayerV3(targetw);
                                pc.SetKillCooldown();
                                pc.Notify(GetString("WarlockControlKill"));
                            }
                            _ = new LateTask(() => { pc.RpcRevertShapeshift(false); }, 1.5f, "Warlock RpcRevertShapeshift");
                        }
                        else
                        {
                            pc.Notify(GetString("WarlockNoTarget"));
                        }
                        Main.isCurseAndKill[pc.PlayerId] = false;
                    }
                    Main.CursedPlayers[pc.PlayerId] = null;
                }
                break;
            case CustomRoles.Assassin:
                if (Main.AssassinCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Assassin.OnShapeshift(pc, true);
                Main.AssassinCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.Undertaker:
                if (Main.UndertakerCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Undertaker.OnShapeshift(pc, true);
                Main.UndertakerCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.Miner:
                if (Main.MinerCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                if (Main.LastEnteredVent.ContainsKey(pc.PlayerId))
                {
                    int ventId = Main.LastEnteredVent[pc.PlayerId].Id;
                    var vent = Main.LastEnteredVent[pc.PlayerId];
                    var position = Main.LastEnteredVentLocation[pc.PlayerId];
                    Logger.Msg($"{pc.GetNameWithRole()}:{position}", "MinerTeleport");
                    Utils.TP(pc.NetTransform, new UnityEngine.Vector2(position.x, position.y));
                }
                Main.MinerCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.Escapee:
                if (Main.EscapeeCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                if (Main.EscapeeLocation.ContainsKey(pc.PlayerId))
                {
                    var position = Main.EscapeeLocation[pc.PlayerId];
                    Main.EscapeeLocation.Remove(pc.PlayerId);
                    Logger.Msg($"{pc.GetNameWithRole()}:{position}", "EscapeeTeleport");
                    Utils.TP(pc.NetTransform, position);
                    pc.RPCPlayCustomSound("Teleport");
                }
                else
                {
                    Main.EscapeeLocation.Add(pc.PlayerId, pc.GetTruePosition());
                }
                Main.EscapeeCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.RiftMaker:
                RiftMaker.OnShapeshift(pc, true);
                break;
            case CustomRoles.Bomber:
                if (Main.BomberCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Logger.Info("炸弹爆炸了", "Boom");
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (var tg in Main.AllPlayerControls)
                {
                    if (!tg.IsModClient()) tg.KillFlash();
                    var pos = pc.transform.position;
                    var dis = UnityEngine.Vector2.Distance(pos, tg.transform.position);

                    if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || (tg.Is(CustomRoleTypes.Impostor) && Options.ImpostorsSurviveBombs.GetBool()) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                    if (dis > Options.BomberRadius.GetFloat()) continue;
                    if (tg.PlayerId == pc.PlayerId) continue;

                    Main.PlayerStates[tg.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    tg.SetRealKiller(pc);
                    tg.RpcMurderPlayerV3(tg);
                    Medic.IsDead(tg);
                }
                _ = new LateTask(() =>
                {
                    bool totalAlive = Main.AllAlivePlayerControls.Any();
                    if (Options.BomberDiesInExplosion.GetBool() && totalAlive && !GameStates.IsEnded)
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        pc.RpcMurderPlayerV3(pc);
                    }
                    Utils.NotifyRoles();
                }, 1.5f, "Bomber Suiscide");
                Main.BomberCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.Nuker:
                if (Main.NukerCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Logger.Info("炸弹爆炸了", "Boom");
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (var tg in Main.AllPlayerControls)
                {
                    if (!tg.IsModClient()) tg.KillFlash();
                    var pos = pc.transform.position;
                    var dis = UnityEngine.Vector2.Distance(pos, tg.transform.position);

                    if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                    if (dis > Options.NukeRadius.GetFloat()) continue;
                    if (tg.PlayerId == pc.PlayerId) continue;

                    Main.PlayerStates[tg.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    tg.SetRealKiller(pc);
                    tg.RpcMurderPlayerV3(tg);
                    Medic.IsDead(tg);
                }
                _ = new LateTask(() =>
                {
                    bool totalAlive = Main.AllAlivePlayerControls.Any();
                    if (totalAlive && !GameStates.IsEnded)
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                        pc.RpcMurderPlayerV3(pc);
                    }
                    Utils.NotifyRoles();
                }, 1.5f, "Nuke");
                Main.NukerCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.QuickShooter:
                if (Main.QuickShooterCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                QuickShooter.OnShapeshift(pc, true);
                Main.QuickShooterCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.Disperser:
                if (Main.DisperserCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Disperser.DispersePlayers(pc);
                Main.DisperserCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
            case CustomRoles.Twister:
                if (Main.TwisterCD.ContainsKey(pc.PlayerId))
                {
                    //if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("AbilityOnCooldown"));
                    break;
                }
                Twister.TwistPlayers(pc, true);
                Main.TwisterCD.TryAdd(pc.PlayerId, Utils.GetTimeStamp());
                break;
        }
    }
}