using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MyceliumNetworking;
using Photon.Pun;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using VoiceRecognitionAPI;

namespace VoiceContent;

[BepInDependency("me.loaforc.voicerecognitionapi", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("RugbugRedfern.MyceliumNetworking", BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin(modGUID, modName, modVersion)]
public class VoiceContent : BaseUnityPlugin
{
    public const string modGUID = "Notest.VoiceContent";
    public const string modName = "VoiceContent";
    public const string modVersion = "1.0.0";
    public const uint modID = 2215935315;
    public static VoiceContent Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    private string[] cussWords = { 
        "fuck me", 
        "fuck this",
        "fuck",
        "shit",
        "holy fuck",
        "bloody hell",
        "cunt",
        "bitch",
        "bastard",
        "asshole",
        "bullshit",
        "cock",
        "cocksucker",
        "twat",
        "wanker",
        "bellend",
        "slut",
        "prick",
        "pussy",
        "motherfucker",
        "hell",
        "balls",
    };

    private string[] youtuberPhrases = {
        "like and subscribe",
        "hit that notification bell",
        "hit that like button",
        "don't forget to share",
        "hit that subscribe button",
        "like comment and subscribe",
        "don't forget to subscribe",
        "let's jump right into it",
        "give this video a thumbs up",
        "click that like button",
        "click the like button",
        "smash that like button",
        "smash the like button",
        "click that notification bell",
        "click the notification bell",
        "smash that notification bell",
        "smash the notification bell",
        "click that subscribe button",
        "click the subscribe button",
        "smash that subscribe button",
        "smash the subscribe button",
        "before starting this video",
    };

    private string[] sponsorPhrases = // Don't add words like "honey" or "sofi" because these can be used in day to day conversations
    {
        "this video is sponsored by",
        "i want to give a huge shoutout to our sponsor",
        "i want to give a shoutout to our sponsor",
        "this video is made possible by",
        "today's episode is brought to you by",
        "today's episode is made possible by",
        "i want to take a quick moment to thank",
        "this video is brought to you by",
        "before we begin i want to thank",
        "with my promo code",
        "with my discount code",
        "with my star code",
        "with my creator code",
        "use my promo code",
        "use my discount code",
        "use my star code",
        "use my creator code",
        "patreon",
        "kofi",
        "gfuel",
        "temu",
        "nordvpn",
        "private internet access",
        "expressvpn",
        "audible",
        "skillshare",
        "squarespace",
        "raid shadow legends",
        "raycon",
        "hello fresh",
        "manscaped",
        "betterhelp",
        "grammarly",
        "blue apron",
        "dollar shave club",
        "rocket money",
        "fortnite",
        "honkai star rail",
        "genshin impact",
        "paypal",
    };

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        Patch();

        Logger.LogInfo($"{modGUID} v{modVersion} has loaded!");
        MyceliumNetwork.RegisterNetworkObject(this, modID);

        Voice.ListenForPhrases(youtuberPhrases, (message) => {
            Logger.LogInfo("YouTuber phrase was said");
            HandlePhrase("like");
        });

        Voice.ListenForPhrases(sponsorPhrases, (message) => {
            Logger.LogInfo("Sponsor segment phrase was said");
            HandlePhrase("sponsor");
        });

        Voice.ListenForPhrases(cussWords, (message) => {
            Logger.LogInfo("Cuss word was said");
            HandlePhrase("cuss");
        });
    }

    private bool GetPlayerWithCamera(out Photon.Realtime.Player? playerOut)
    {
        Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
        int i = 0;
        while (i < playerList.Length)
        {
            Photon.Realtime.Player player = playerList[i];
            GlobalPlayerData globalPlayerData;
            if (GlobalPlayerData.TryGetPlayerData(player, out globalPlayerData))
            {
                using (List<ItemDescriptor>.Enumerator enumerator = globalPlayerData.inventory.GetItems().GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.item.name == "Camera")
                        {
                            playerOut = player;
                            Logger.LogDebug("Found Camera! " + player.NickName + " Has it!");

                            return true;
                        }
                    }
                    goto IL_A2;
                }
            }
            goto IL_88;
            IL_A2:
            i++;
            continue;
             IL_88:
            Debug.LogError("Cant find playerData for Player: " + player.NickName + " Bug!?");
            goto IL_A2;
        }
        playerOut = null;
        return false;
    }
    
    private void HandlePhrase(string phraseType)
    {
        Photon.Realtime.Player? playerOut;
        bool cameraSuccess = GetPlayerWithCamera(out playerOut);

        if (cameraSuccess)
        {
            if (playerOut!.IsLocal)
            {
                Logger.LogDebug("Local player is the one holding the camera, creating provider");
                CreateProvider(phraseType);
            }
            else
            {
                Logger.LogDebug("Local player is not the one holding the camera");
                CSteamID steamID;
                bool idSuccess = SteamAvatarHandler.TryGetSteamIDForPlayer(playerOut, out steamID);
                if (idSuccess)
                {
                    Logger.LogDebug("Got steamID successfully");
                    /*
                     * From prior testing I'm sure only the camera man needs to create the provider
                     * But if extensive testing comes out inconclusive I might just give up and RPC providers to everyone always
                    */
                    MyceliumNetwork.RPCTarget(modID, nameof(ReplicateProvider), steamID, ReliableType.Reliable, phraseType);
                }
                else
                {
                    Logger.LogDebug("Could not get SteamId");
                }
                //MyceliumNetwork.RPC(modID, nameof(ReplicateProvider), ReliableType.Reliable, phraseType); 
                CreateProvider(phraseType);
            }
        }
        else
        {
            Logger.LogDebug("Could not get player holding camera, if any");
        }
    }

    [CustomRPC]
    private void ReplicateProvider(string type)
    {
        Logger.LogDebug($"Asked to replicate provider of type {type}");
        CreateProvider(type);
    }

    private void CreateProvider(string type)
    {
        VoiceContentProvider componentInParent = new VoiceContentProvider(type);
        if (!ContentPolling.contentProviders.TryAdd(componentInParent, 400000))
        {
            Dictionary<ContentProvider, int> dictionary = ContentPolling.contentProviders;
            ContentProvider key = componentInParent;
            int seenAmount = dictionary[key];
            dictionary[key] = seenAmount + 400000;
        }
    }
    internal static void Patch()
    {
        Harmony ??= new Harmony(modGUID);

        Logger.LogDebug("Patching...");

        Harmony.PatchAll();

        Logger.LogDebug("Finished patching!");
    }

    internal static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Logger.LogDebug("Finished unpatching!");
    }
}