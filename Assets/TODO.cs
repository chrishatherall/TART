using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TODO : MonoBehaviour
{
    public string[] todo = {
        // ##### NEXT ####
        "Deathmatch - killing with respawn, endless",
        "random gun spawns, with a loot table ref on each spawn", // define loot tables on GM, then ref to those on spawner
        "death animation/visualisation, with respawn button",
        "respawn ability",
        "self-heal ability",
        "show rig to other players, with IK on gun",
        "aim head at cursor viewpoint",
        "death ragdoll",
        "Proper tab UI with player list/info",
        "escape menu with resume/options/quit",
        "ctrl shouldnt fire",
        //"Go through lighting guide before making too much map",
        //"Door open/close sounds",
        //"Pickup sound",
        //"Better alerts",
        //"Start map from lobby screen to enable photon, use CREATE random room so each session is private",
        //"shouldnt be able to uncrouch if theres something above you",
        //"tooltip has zbuffer issues",
        //"camera near plane and door trim collider",
        //"pickup angle bug",

        // #### MAP ####
        "Office floor UV",
        "traitor room windows",
        "bedroom floor thin gap",

        // ##### GUNPLAY #####
        //"Guns cant reload",
        "Guns look like arse",
        //"Recoil direction should be based on the gun orientation",
        "In addition to recoil, we need inaccuracy and firing error (all directions)",
        "bullet holes decay, and holes on top of holes aren't removed",

        // GAMEPLAY
        "diagonal movement is faster than one direction",
        "if crouching while in air, dont move camera down, move everything _else_ up",
        "Vents can't open 90, they're stuck at 88 because of a gimble lock issue?",
        //"mouse look is awful and jittery, need mouse smoothing?",
        "Roles need to be selected properly, instead of randomly",
        "picked up items aren't network instantiated",
        //"doors have a rotation issue. Local rot instead of global? - on activation call, add vector3 of hit, this should let door script know which side is being opened",
        //"can look down more than vertical",
        //"raycast issue with looking at activatables/pickups",
        "all players are in the LocalPlayer layer, not just the local player",
        // ##### NETWORKING #####
        //"Sync problem with roles",
        "round complete requirements need to include minimum round time",
        "gun reloading isnt synced"
    };
     
}
