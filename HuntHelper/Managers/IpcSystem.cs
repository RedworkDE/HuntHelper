﻿using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using HuntHelper.Gui;
using HuntHelper.Managers.Hunts;
using HuntHelper.Managers.Hunts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HuntHelper.Managers;

public class IpcSystem : IDisposable
{
    private const uint HuntHelperApiVersion = 1;
    private const string IpiFuncNameEnable = "HH.Enable";
    private const string IpcFuncNameDisable = "HH.Disable";
    private const string IpcFuncNameGetVersion = "HH.GetVersion";
    private const string IpcFuncNameGetTrainList = "HH.GetTrainList";
    private const string IpcFuncNameImportTrainList = "HH.ImportTrainList";

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IFramework _framework;
    private readonly TrainManager _trainManager;

    private readonly ICallGateProvider<uint> _cgGetVersion;
    private readonly ICallGateProvider<List<MobRecord>> _cgGetTrainList;
    private readonly ICallGateProvider<List<MobRecord>,bool> _cgImportTrainList;

    public IpcSystem(DalamudPluginInterface pluginInterface, IFramework framework, TrainManager trainManager)
    {
        _pluginInterface = pluginInterface;
        _framework = framework;
        _trainManager = trainManager;

        _cgGetVersion = pluginInterface.GetIpcProvider<uint>(IpcFuncNameGetVersion);
        _cgGetTrainList = pluginInterface.GetIpcProvider<List<MobRecord>>(IpcFuncNameGetTrainList);
        _cgImportTrainList = pluginInterface.GetIpcProvider<List<MobRecord>,bool>(IpcFuncNameImportTrainList);

        _cgGetVersion.RegisterFunc(GetVersion);
        _cgGetTrainList.RegisterFunc(GetTrainList);
        _cgImportTrainList.RegisterAction(ImportTrainList);

        pluginInterface.GetIpcProvider<uint, bool>(IpiFuncNameEnable).SendMessage(HuntHelperApiVersion);
    }

    public void Dispose()
    {
        _cgGetVersion.UnregisterFunc();
        _cgGetTrainList.UnregisterFunc();
        _cgImportTrainList.UnregisterAction();

        _pluginInterface.GetIpcProvider<bool>(IpcFuncNameDisable).SendMessage();
    }

    private static uint GetVersion() => HuntHelperApiVersion;

    private List<MobRecord> GetTrainList() =>
        _framework.RunOnFrameworkThread(() =>
            _trainManager.HuntTrain.Select(AsMobRecord).ToList()
        ).Result;
    
    private void ImportTrainList(List<MobRecord> trainList)
    {
        _trainManager.ImportFromIPC = true;
        /* if we rework this and use a bool instead
         * since train manager is used multiple times in this class, but hunt train ui is only used for this
         * we can rework it so hunt train ui is not needed
         * 
         * we can then check the bool in the UI
         * 
         * separating things
        */

        _trainManager.Import(trainList.Select(FromMobRecord).ToList());
        //_huntTrainUi.OpenImportPopup();
    }

    private static MobRecord AsMobRecord(HuntTrainMob mob) =>
        new MobRecord(mob.Name, mob.MobID, mob.TerritoryID, mob.MapID, mob.Instance, mob.Position, mob.Dead, mob.LastSeenUTC);

    private static HuntTrainMob FromMobRecord(MobRecord mob) =>
        new HuntTrainMob(mob.Name, mob.MobID, mob.TerritoryID, mob.MapID, mob.Instance, string.Empty,  mob.Position, mob.LastSeenUTC, mob.Dead);

    private record struct MobRecord(
        string Name,
        uint MobID,
        uint TerritoryID,
        uint MapID,
        uint Instance,
        Vector2 Position,
        bool Dead,
        DateTime LastSeenUTC
    );
}
