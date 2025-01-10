using CommandSystem;
using Interactables.Interobjects.DoorUtils;
using Interactables.Interobjects;
using InventorySystem.Items.Pickups;
using PlayerRoles;
using PluginAPI.Core.Attributes;
using PluginAPI.Core;
using PluginAPI.Enums;
using PluginAPI.Events;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using System.Threading.Tasks;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class DogHuntCommand : ICommand
{
    private static readonly List<string> HeavyRooms = new List<string>
    {
        "096", "HCZ_ARMORY", "HID_CHAMBER", "HID_UPPER", "HID_LOWER", "CHECKPOINT_EZ_HCA_A"
    };

    private readonly HashSet<Player> activeDogs = new HashSet<Player>();
    private float scp939Timer = 120f;

    public string Command => "DogHunt";
    public string[] Aliases => new string[] { "doghunt" };
    public string Description => "One player starts as SCP-939 and must hunt Chaos players. When a Chaos player dies, they turn into SCP-939.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!(sender is CommandSender val) || !Misc.CheckPermission(val, PlayerPermissions.FacilityManagement))
        {
            response = "You do not have permission to run this command.";
            return false;
        }

        List<Player> players = Player.GetPlayers().Where(p => p != null).ToList();
        if (players.Count < 2)
        {
            response = "Not enough players to start Dog Hunt!";
            return false;
        }

        ResetGameState();

        System.Random random = new System.Random();
        Player scp939 = players.OrderBy(x => random.Next()).FirstOrDefault();
        if (scp939 == null)
        {
            response = "Failed to select SCP-939!";
            return false;
        }

        List<Player> chaosPlayers = players.Where(p => p.Role != RoleTypeId.Scp939).Except(new[] { scp939 }).ToList();

        OpenAllHeavyDoors();
        LockAllCheckpoints();
        LockAllElevators();
        DestroyAllItems();

        DoorVariant spawnDoor = Room939();
        scp939.Position = spawnDoor != null ? spawnDoor.transform.position : Vector3.zero;

        activeDogs.Add(scp939);
        InitializeSCP939State(scp939);
        SetChaosPlayers(chaosPlayers);

        foreach (var player in players)
        {
            player.ClearInventory();
        }

        Map.ForceDecontamination();
        StartTimersAsync();
        response = "Dog Hunt has started! SCP-939 is hunting for Chaos Insurgents in Heavy Containment.";

        return true;
    }

    private async void StartTimersAsync()
    {
        await SCP939TimerCountdownAsync();
    }

    private async Task SCP939TimerCountdownAsync()
    {
        for (int i = 120; i > 0; i--)
        {
            foreach (var player in Player.GetPlayers())
            {
                player.SendBroadcast($"The dog will be released in {i} seconds!", 1);
            }
            await Task.Delay(1000);
        }

        foreach (var player in Player.GetPlayers())
        {
            player.SendBroadcast("The dog has been released!", 5);
        }
    }

    private void ResetGameState()
    {
        activeDogs.Clear();
        foreach (var player in Player.GetPlayers())
        {
            player.SetRole(RoleTypeId.Spectator);
            player.Position = Vector3.zero;
        }
    }

    private void OpenAllHeavyDoors()
    {
        foreach (var door in DoorVariant.AllDoors)
        {
            if (door != null)
            {
                door.NetworkTargetState = true;
                door.ServerChangeLock(DoorLockReason.AdminCommand, true);
            }
        }
    }

    private void LockAllCheckpoints()
    {
        foreach (var door in DoorVariant.AllDoors.OfType<CheckpointDoor>())
        {
            door.ServerChangeLock(DoorLockReason.AdminCommand, true);
        }
    }

    private void LockAllElevators()
    {
        foreach (var door in DoorVariant.AllDoors.OfType<ElevatorDoor>())
        {
            door.NetworkTargetState = false;
        }
    }

    private void DestroyAllItems()
    {
        foreach (var item in UnityEngine.Object.FindObjectsOfType<ItemPickupBase>())
        {
            if (item != null)
            {
                item.DestroySelf();
            }
        }
    }

    private void InitializeSCP939State(Player scp939)
    {
        scp939.SetRole(RoleTypeId.Scp939);
        scp939.EffectsManager.ChangeState("Flashed", 255, 120f);
        scp939.EffectsManager.ChangeState("Slowness", 100, 120f);
        scp939.SendBroadcast("Now go catch the Chaos", 5);
    }

    private void SetChaosPlayers(List<Player> chaosPlayers)
    {
        System.Random random = new System.Random();
        var availableRooms = new List<string>(HeavyRooms);

        foreach (Player player in chaosPlayers)
        {
            player.SetRole(RoleTypeId.ChaosConscript);

            if (availableRooms.Count == 0)
            {
                player.Position = Vector3.zero;
                continue;
            }

            string selectedRoom = availableRooms[random.Next(availableRooms.Count)];
            availableRooms.Remove(selectedRoom);

            player.Position = GetRoomPosition(selectedRoom);
        }
    }

    private Vector3 GetRoomPosition(string roomName)
    {
        DoorVariant roomDoor = DoorVariant.AllDoors.FirstOrDefault(d => d.DoorName == roomName);
        return roomDoor != null ? roomDoor.transform.position + new Vector3(0f, 1f, 0f) : Vector3.zero;
    }

    private DoorVariant Room939()
    {
        return DoorVariant.AllDoors.FirstOrDefault(d => d.DoorName == "939_CRYO");
    }
}
