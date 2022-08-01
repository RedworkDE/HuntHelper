﻿using ImGuiNET;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Plugin;
using HuntHelper.MapInfoManager;
using Lumina.Excel.GeneratedSheets;
using HuntHelper.Managers.Hunts;

namespace HuntHelper
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;
        private DalamudPluginInterface pluginInterface;

        private ImGuiScene.TextureWrap goatImage;

        private ClientState ClientState;
        private ObjectTable ObjectTable;
        private DataManager DataManager;
        private HuntManager HuntManager;
        private MapDataManager mapDataManager;

        private String TerritoryName;
        private ushort TerritoryID;

        private float MobIconRadius;
        private float PlayerIconRadius;
        private float SpawnPointIconRadius;
        private float bottomPanelHeight = 80;

        private float detectionRadiusModifier = 1.0f;
        private float mouseOverDistanceModifier = 2.5f;
        private float iconRadiusModifier = 1.0f; //let user choose custom modifier for icons
        //initial window position
        private Vector2 mapWindowPos = new Vector2(25, 25);


        private uint spawnPointColour = ImGui.ColorConvertFloat4ToU32(new Vector4(0.69f, 0.69f, 0.69f, 1f));
        private uint mobColour = ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 1f, 0.567f, 1f));
        private uint playerColour = ImGui.ColorConvertFloat4ToU32(new Vector4(.5f, 0.567f, 1f, 1f));


        // this extra bool exists for ImGui, since you can't ref a property
        private bool mainWindowVisible = false;
        private bool ShowDatabaseListWindow = false;
        private bool showDebug = false;
        private bool _showOptionsWindow = true;

        public bool MainWindowVisible
        {
            get { return this.mainWindowVisible; }
            set { this.mainWindowVisible = value; }
        }

        private bool testVisible = false;
        public bool TestVisible
        {
            get => testVisible;
            set => testVisible = value;
        }

        private bool settingsVisible = false;

        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }


        public PluginUI(Configuration configuration, DalamudPluginInterface pluginInterface,
            ClientState clientState, ObjectTable objectTable, DataManager dataManager,
            HuntManager huntManager, MapDataManager mapDataManager)
        {
            //add hunt manager to this class

            this.configuration = configuration;
            this.pluginInterface = pluginInterface; //not using atm...
            //this.goatImage = goatImage;

            this.ClientState = clientState;
            this.ObjectTable = objectTable;
            this.DataManager = dataManager;
            this.HuntManager = huntManager;
            this.mapDataManager = mapDataManager;

            TerritoryName = String.Empty;
            ClientState_TerritoryChanged(null, 0);

            ClientState.TerritoryChanged += ClientState_TerritoryChanged;


        }

        public void Dispose()
        {
            //this.goatImage.Dispose();
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow();
            DrawSettingsWindow();


            DrawTestWindow();
        }

        public void DrawMainWindow()
        {
            if (!MainWindowVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 330), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("My Amazing Window!", ref this.mainWindowVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text($"The random config bool is {this.configuration.SomePropertyToBeSavedAndWithADefault}");

                if (ImGui.Button("Settings"))
                {
                    SettingsVisible = true;
                }

                ImGui.Spacing();

                ImGui.Indent(55);
                //ImGui.Image(this.goatImage.ImGuiHandle, new Vector2(this.goatImage.Width, this.goatImage.Height));
                ImGui.Unindent(55);

                ImGui.Text($"Territory: {TerritoryName}");
                ImGui.Text($"Territory ID: {ClientState.TerritoryType}");

                //PLAYER POS
                var v3 = ClientState.LocalPlayer?.Position ?? new Vector3(0, 0, 0);
                ImGui.Text($"v3: -----\n" +
                           $"X: {ConvertPosToCoordinate(v3.X)} |" +
                           $"Y: {ConvertPosToCoordinate(v3.Z)} |\n" +
                           $"v3: -----\n" +
                           $"X: {Math.Round(v3.X, 2)} |" +
                           $"Y: {Math.Round(v3.Z, 2)} |");
                //END
                
                ImGui.Indent(55);
                ImGui.Text("");

                var hunt = "";
                foreach (var obj in this.ObjectTable)
                {
                    if (obj is not BattleNpc bobj) continue;
                    if (bobj.MaxHp < 10000) continue; //not really needed if subkind is enemy, once matching to id / name
                    if (bobj.BattleNpcKind != BattleNpcSubKind.Enemy) continue; //not really needed if matching to 'nameID'


                    hunt += $"{obj.Name} \n" +
                            $"KIND: {bobj.BattleNpcKind}\n" +
                            $"NAMEID: {bobj.NameId}\n" +
                            $"|HP: {bobj.CurrentHp}\n" +
                            $"|HP%%: {(bobj.CurrentHp * 1.0 / bobj.MaxHp) * 100}%%\n" +
                            //$"|object ID: {obj.ObjectId}\n| Data ID: {obj.DataId} \n| OwnerID: {obj.OwnerId}\n" +
                            $"X: {ConvertPosToCoordinate(obj.Position.X)};\n" +
                            $"Y: {ConvertPosToCoordinate(obj.Position.Y)}\n" +
                            $"  --------------  \n";
                }
                ImGui.Text($"Found: {hunt}");
            }
            ImGui.End();
        }

        public void DrawTestWindow()
        {
            if (!TestVisible)
            {
                return;
            }
            ImGui.SetNextWindowSize(new Vector2(512, 512), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(512, -1), new Vector2(float.MaxValue, -1)); //disable manual resize vertical
            ImGui.SetNextWindowPos(mapWindowPos, ImGuiCond.FirstUseEver);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
            if (ImGui.Begin("Test Window!", ref this.testVisible,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar))
            {
                //if custom size not used, use these default sizes - resize with window size
                //radius for mob / spawn point circles - equal to half a map coord size
                MobIconRadius = iconRadiusModifier * (0.25f * ImGui.GetWindowSize().X / 41);
                SpawnPointIconRadius = MobIconRadius;
                PlayerIconRadius = MobIconRadius / 2; //half of mob icon size


                //change height as width changes, to maintain 1:1 ratio. 
                var width = ImGui.GetWindowSize().X;
                ImGui.SetWindowSize(new Vector2(width));

                var bottomDockingPos = Vector2.Add(ImGui.GetWindowPos(), new Vector2(0, ImGui.GetWindowSize().Y));

                //=========================================
                //if using images
                //draw images first so they are at the bottom.
                //=========================================

                //show map coordinates when mouse is over gui
                ShowCoordOnMouseOver();

                //draw spawn points for the current map, if applicable.
                DrawSpawnPoints(TerritoryID);

                UpdateMobInfo();

                //draw player icon and info
                UpdatePlayerInfo();

                //bottom docking window with buttons and options and stuff
                if (_showOptionsWindow) DrawOptionsWindow();

                //putting this here instead because I want to draw it on this window, not a new one.
                if (showDebug) ShowDebugInfo();

                if (HuntManager.ErrorPopUpVisible || mapDataManager.ErrorPopUpVisible)
                {
                    ImGui.Begin("Error loading data");
                    ImGui.Text(HuntManager.ErrorMessage);
                    ImGui.Text(mapDataManager.ErrorMessage);
                    ImGui.End();
                }


                var cursorPos = new Vector2(8, ImGui.GetWindowSize().Y-30);
                ImGui.SetCursorPos(cursorPos);
                if (ImGui.Button(" ö "))//get a cogwheel img or something idk
                {
                    _showOptionsWindow = !_showOptionsWindow;
                } 
            }
            ImGui.End();
        }

        private void DrawOptionsWindow()
        {
            #region Bottom docking info window
            var bottomDockingPos = Vector2.Add(ImGui.GetWindowPos(), new Vector2(0, ImGui.GetWindowSize().Y));
            //Current mob info 'docking'
            ImGui.BeginChild(1, new Vector2(ImGui.GetWindowSize().X, 25));
            ImGui.SetNextWindowSize(new Vector2(ImGui.GetWindowSize().X, bottomPanelHeight), ImGuiCond.None);
            ImGui.SetNextWindowSizeConstraints(new Vector2(-1, 0), new Vector2(-1, float.MaxValue));

            //hide grip color
            ImGui.PushStyleColor(ImGuiCol.ResizeGrip, 0);

            ImGui.Begin("Options Window", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove);
            ImGui.SetWindowPos(bottomDockingPos);
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Columns(2);
            ImGui.SetColumnWidth(0, ImGui.GetWindowSize().X / 3);
            ImGui.SetColumnWidth(1, 2 * ImGui.GetWindowSize().X / 3);
            if (ImGui.Button("Show Debug")) showDebug = !showDebug;
            DrawDataBaseWindow();
            ImGui.NextColumn();
            ImGui.TextUnformatted("Add some toggle options and stuff here?");
            ImGui.TextUnformatted("FDSFD");
            ImGui.TextUnformatted("HELLO %%%%%%% TEXT");

            bottomPanelHeight = ImGui.GetWindowSize().Y;
            ImGui.End();
            ImGui.EndChildFrame();
            #endregion
        }
        public void DrawSettingsWindow()
        {
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(232, 75), ImGuiCond.Always);
            if (ImGui.Begin("A Wonderful Configuration Window", ref this.settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                // can't ref a property, so use a local copy
                var configValue = this.configuration.SomePropertyToBeSavedAndWithADefault;
                if (ImGui.Checkbox("Random Config Bool", ref configValue))
                {
                    this.configuration.SomePropertyToBeSavedAndWithADefault = configValue;
                    // can save immediately on change, if you don't want to provide a "Save and Close" button
                    this.configuration.Save();
                }
                DrawDataBaseWindow();
            }
            ImGui.End();
        }


        private void ClientState_TerritoryChanged(object? sender, ushort e)
        {
            TerritoryName = Utilities.MapHelpers.GetMapName(DataManager, this.ClientState.TerritoryType);
            TerritoryID = ClientState.TerritoryType;
        }

        #region Draw Sub Windows

        private void DrawDataBaseWindow()
        {
            if (ImGui.Button("Show Loaded Hunt Data"))
            {
                ShowDatabaseListWindow = !ShowDatabaseListWindow;
            }

            if (!ShowDatabaseListWindow) return;

            ImGui.SetNextWindowSize(new Vector2(450, 800));
            ImGui.Begin("Loaded Hunt Data", ref ShowDatabaseListWindow);
            if (ImGui.BeginTabBar("info"))
            {
                if (ImGui.BeginTabItem("database"))
                {
                    ImGui.PushFont(UiBuilder.MonoFont);
                    ImGui_CentreText(HuntManager.GetDatabaseAsString());
                    ImGui.PopFont();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("spawn points"))
                {
                    ImGui.TextUnformatted(mapDataManager.ToString());
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        #endregion

        #region DrawList Stuff - Mob Icon, Player Icon
        public void DrawSpawnPoints(ushort mapID)
        {
            var spawnPoints = mapDataManager.GetSpawnPoints(mapID);
            if (spawnPoints.Count == 0) return;

            var drawList = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();

            foreach (var sp in spawnPoints)
            {
                var drawPos = CoordinateToPositionInWindow(sp);
                drawList.AddCircleFilled(drawPos, SpawnPointIconRadius, spawnPointColour);
            }
        }

        #region Mob


        private void UpdateMobInfo()
        {
            var drawlist = ImGui.GetWindowDrawList();
            HuntManager.ClearMobs();
            foreach (var obj in this.ObjectTable)
            {
                if (obj is not BattleNpc mob) continue;
                if (!HuntManager.IsHunt(mob.NameId)) continue;
                HuntManager.AddMob(mob);
                DrawMobIcon(mob);
            }

            DrawPriorityMobInfo();
        }

        private void DrawMobIcon(BattleNpc mob)
        {
            var mobPos = CoordinateToPositionInWindow(new Vector2(ConvertPosToCoordinate(mob.Position.X),
                ConvertPosToCoordinate(mob.Position.Z)));
            var drawlist = ImGui.GetWindowDrawList();
            drawlist.AddCircleFilled(mobPos, MobIconRadius, mobColour);

            //draw mob icon tooltip
            if (Vector2.Distance(ImGui.GetMousePos(), mobPos) < MobIconRadius * mouseOverDistanceModifier)
            {
                DrawMobIconToolTip(mob);
            }
        }

        private void DrawMobIconToolTip(BattleNpc mob)
        {
            var text = new string[]
            {
                $"{mob.Name}",
                $"({ConvertPosToCoordinate(mob.Position.X)}, {ConvertPosToCoordinate(mob.Position.Z)})",
                $"{Math.Round((mob.CurrentHp * 1.0) / mob.MaxHp * 100, 2)}%"
            };
            Imgui_ToolTip(text);
        }

        private void DrawPriorityMobInfo()
        {
            var (rank, mob) = HuntManager.GetPriorityMob();
            if (mob == null) return;
            ImGui.PushFont(UiBuilder.MonoFont);
            ImGui.Spacing();
            ImGui_CentreText($"   A     |  {mob.Name}  |  {Math.Round(((1.0 * mob.CurrentHp) / mob.MaxHp) * 100):0.00}%");
            ImGui_CentreText($"({ConvertPosToCoordinate(mob.Position.X):0.00}, {ConvertPosToCoordinate(mob.Position.Z):0.00})");
            ImGui.PopFont();
        }
        #endregion

        #region Player
        private void UpdatePlayerInfo()
        {
            //if this stuff ain't null, draw player position
            if (ClientState?.LocalPlayer?.Position != null)
            {
                //Todo - make these easier to read...
                var playerPos = CoordinateToPositionInWindow(
                    new Vector2(ConvertPosToCoordinate(ClientState.LocalPlayer.Position.X),
                        ConvertPosToCoordinate(ClientState.LocalPlayer.Position.Z)));

                //aoe detection circle = ~2 in-game coords. --DRAWN BELOW
                var detectionRadius = 2 * (ImGui.GetContentRegionAvail().X / 41) * detectionRadiusModifier;
                var rotation = Math.Abs(ClientState.LocalPlayer.Rotation - Math.PI);
                var lineEnding =
                    new Vector2(
                        playerPos.X + (float)(detectionRadius * Math.Sin(rotation)),
                        playerPos.Y - (float)(detectionRadius * Math.Cos(rotation)));

                #region Player Icon Drawing - order: direction, detection, player

                var drawlist = ImGui.GetWindowDrawList();
                DrawPlayerIcon(drawlist, playerPos, detectionRadius, lineEnding);

                #endregion
            }

        }

        private void DrawPlayerIcon(ImDrawListPtr drawlist, Vector2 playerPos, float detectionRadius, Vector2 lineEnding)
        {
            //direction line
            drawlist.AddLine(playerPos, lineEnding, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.3f, 0.3f, 1f)), 2);
            //player filled circle
            drawlist.AddCircleFilled(playerPos, PlayerIconRadius,
                playerColour);
            //detection circle
            drawlist.AddCircle(playerPos, detectionRadius,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 0f, 1f)), 0, 1f);

        }
        #endregion

        #endregion

        private void ShowCoordOnMouseOver()
        {
            var winPos = ImGui.GetWindowPos();
            var winSize = ImGui.GetWindowSize();
            var mousePos = ImGui.GetMousePos();

            //if mouse pos is between top left (window pos) and bottom right of window (window pos + window size)
            if (Vector2.Subtract(mousePos, winPos).X > 0 && Vector2.Subtract(mousePos, winPos).Y > 0 &&
                Vector2.Subtract(mousePos, Vector2.Add(winPos, winSize)).X < 0 && Vector2.Subtract(mousePos, Vector2.Add(winPos, winSize)).Y < 0)
            {
                var coord = MouseOverPositionToGameCoordinate();
                var text = new string[] { $"({Math.Round(coord.X, 2)}, {Math.Round(coord.Y, 2)})" };
                Imgui_ToolTip(text);
            }
        }

        private void ShowDebugInfo()
        {
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Text("Random Debug Info?!:");
            ImGui.Columns(2);
            ImGui.Text($"Content1 Region: {ImGui.GetContentRegionAvail()}");
            ImGui.Text($"Window Size: {ImGui.GetWindowSize()}");

            if (ClientState?.LocalPlayer?.Position != null)
            {
                ImGui.Text($"rotation: {ClientState.LocalPlayer.Rotation}");
                var rotation = Math.Abs(ClientState.LocalPlayer.Rotation - Math.PI);
                ImGui.Text($"rotation rad.: {Math.Round(rotation, 2):0.00}");
                ImGui.Text($"rotation sin.: {Math.Round(Math.Sin(rotation), 2):0.00}");
                ImGui.Text($"rotation cos.: {Math.Round(Math.Cos(rotation), 2):0.00}");
                ImGui.Text($"Player Pos: (" +
                           $"{Utilities.MapHelpers.ConvertToMapCoordinate(ClientState.LocalPlayer.Position.X):0.00}" +
                           $"," +
                           $" {Utilities.MapHelpers.ConvertToMapCoordinate(ClientState.LocalPlayer.Position.Z):0.00}" +
                           $")");
                ImGui.NextColumn();
                ImGui_RightAlignText($"Map: {TerritoryName}");
                ImGui_RightAlignText($"Map ID: {TerritoryID}");

                //priority mob stuff
                ImGui_RightAlignText("Priority Mob:");
                var priorityMob = HuntManager.GetPriorityMob().Item2;
                if (priorityMob != null)
                {
                    ImGui_RightAlignText($"Rank {HuntManager.GetPriorityMob().Item1} " +
                                         $"{priorityMob.Name} " +
                                         $"{Math.Round(1.0 * priorityMob.CurrentHp / priorityMob.MaxHp * 100, 2):0.00}% | " +
                                         $"({ConvertPosToCoordinate(priorityMob.Position.X):0.00}, " +
                                         $"{ConvertPosToCoordinate(priorityMob.Position.Z):0.00}) ");
                }

                var currentMobs = HuntManager.GetAllCurrentMobs();
                ImGui_RightAlignText("--");
                ImGui_RightAlignText("Other nearby mobs:");
                foreach (var (rank, mob) in currentMobs)
                {
                    ImGui_RightAlignText("--");
                    ImGui_RightAlignText($"Rank: {rank} | " +
                                         $"{mob.Name} {Math.Round(1.0 * mob.CurrentHp / mob.MaxHp * 100, 2):0.00}% | " +
                                         $"({ConvertPosToCoordinate(mob.Position.X):0.00}, " +
                                         $"{ConvertPosToCoordinate(mob.Position.Z):0.00})");
                }
            }
            else
            {
                ImGui.Text("Can't find local player");
            }
        }

        private void ImGui_CentreText(string text)
        {
            var windowWidth = ImGui.GetWindowSize().X;
            var textWidth = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
            ImGui.TextUnformatted(text);
        }

        private void ImGui_RightAlignText(string text)
        {
            var windowWidth = ImGui.GetWindowSize().X;
            var textWidth = ImGui.CalcTextSize(text).X;
            ImGui.SetCursorPosX((windowWidth - textWidth) * .95f);
            ImGui.TextUnformatted(text);
        }

        //use list instead? fixed input, so slightly more 'optimized' w/ array i guess...
        private void Imgui_ToolTip(string[] text)
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 50.0f);
            ImGui.Separator();

            foreach (var s in text)
            {
                ImGui_CentreText(s);
            }
            ImGui.Separator();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().IndentSpacing * 0.5f);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        //=================================================================

        private Vector2 MouseOverPositionToGameCoordinate()
        {
            var coordinateSize = (ImGui.GetWindowSize().X / 41);
            var mousePos = ImGui.GetMousePos() - ImGui.GetWindowPos();
            var coord = mousePos / coordinateSize + Vector2.One;
            return coord;
        }

        //redundant...
        private float ConvertPosToCoordinate(float pos)
        {
            return Utilities.MapHelpers.ConvertToMapCoordinate(pos);
        }

        private Vector2 CoordinateToPositionInWindow(Vector2 pos)
        {
            var ratio = ImGui.GetWindowSize().X / 41;
            var winPos = ImGui.GetWindowPos();

            var x = (pos.X - 1) * ratio + winPos.X;
            var y = (pos.Y - 1) * ratio + winPos.Y;

            return new Vector2(x, y);
        }

        //z pos offset seems diff for every map, idk idc...
        private double ConvertARR_Z_ToCoordinate(float pos)
        {
            // arr z index seems to be 0 == 12. since: 1.0 == 112 and 0.5 == 62. 
            // not exactly super thorough testing tbh

            //return Math.Floor(((pos - 12) / 100.0) * 100) / 100; //western than
            return Math.Floor(((pos + 1) / 100.0) * 100) / 100; //middle la noscea 
        }
    }
}
