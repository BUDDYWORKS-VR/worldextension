using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.StringLoading;
using UnityEngine.UI;

namespace BUDDYWORKS.WorldExtension
{
  [AddComponentMenu("BUDDYWORKS/Permission Manager")]
  public class PermissionManager : UdonSharpBehaviour
  {
    [Header("Configuration")]
    [Tooltip("URL of the CSV file containing user permissions")]
    public VRCUrl permissionsURL;

    [Header("Role GameObjects")]
    public GameObject[] adminGameObjects;
    public GameObject[] headStaffGameObjects;
    public GameObject[] securityGameObjects;
    public GameObject[] staffGameObjects;
    public GameObject[] dancerGameObjects;
    public GameObject[] patron1GameObjects;
    public GameObject[] patron2GameObjects;

    [Header("Other Properties")]
    [Tooltip("List of usernames to always grant full permissions (except patron roles)")]
    public string[] bypassUsers;
    [Tooltip("Maximum number of Patrons expected, used for array sizing.")]
    public int maxPatrons = 100;
    [Tooltip("Target for the patreon text parsing, this one is for the high-tier patrons.")]
    public Text patreonText1;
    [Tooltip("Target for the patreon text parsing, this one is for regular patrons.")]
    public Text patreonText2;
    [Tooltip("Text field to display the permission information.")]
    public Text permissionDisplay;

    private string _loadedPermissionsData = "";

    private void Start()
    {
      //You could put something here that runs on start.
    }

    void OnEnable()
    {
      LoadPermissions();
    }

    public void LoadPermissions()
    {
      VRCStringDownloader.LoadUrl(permissionsURL, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
      Debug.Log("Permissions loaded successfully.");
      _loadedPermissionsData = result.Result;
      ProcessPermissions();
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
      Debug.LogError("Error loading permissions: " + result.Error);
    }

    private void ProcessPermissions()
    {
      string[] lines = _loadedPermissionsData.Split('\n');

      // Process Patreon List (all users)
      ProcessPatreonLists(lines);

      // Process Local User Permissions
      VRCPlayerApi localPlayer = Networking.LocalPlayer;
      if (localPlayer == null)
      {
        Debug.LogError("Local player is null!");
        return;
      }

      string localUsername = localPlayer.displayName;
      string permissionSource = "Whitelist"; // Default
      string highestPermission = "Rank"; // Default

      // 3. Check for bypass user - EARLY EXIT
      bool bypassed = false;
      if (bypassUsers != null)
      {
        for (int i = 0; i < bypassUsers.Length; i++)
        {
          if (localUsername == bypassUsers[i].Trim())
          {
            Debug.Log($"Local user {localUsername} is bypassed.");
            ApplyBypassPermissions(localPlayer);
            highestPermission = "Admin";
            bypassed = true;
            permissionSource = "Bypassed";
            break;
          }
        }
      }

      if (!bypassed)
      {
        bool hasAnyPermission = false; // Flag to track if ANY permission is granted

        for (int i = 1; i < lines.Length; i++)
        {
          string line = lines[i];
          string[] fields = line.Split(',');

          if (!IsValidPermissionRow(fields))
          {
            continue;
          }

          string username = fields[0].Trim();

          if (username == localUsername)
          {
            bool isAdmin = IsRoleActive(fields[1]);
            bool isHeadStaff = IsRoleActive(fields[2]);
            bool isSecurity = IsRoleActive(fields[3]);
            bool isStaff = IsRoleActive(fields[4]);
            bool isDancer = IsRoleActive(fields[5]);

            Debug.Log(
                $"Processing local user: {username}, isAdmin: {isAdmin}, " +
                $"isHeadStaff: {isHeadStaff}, isSecurity: {isSecurity}, " +
                $"isStaff: {isStaff}, isDancer: {isDancer}");

            ApplyPermissions(
                localPlayer, isAdmin, isHeadStaff, isSecurity, isStaff,
                isDancer);

            if (isAdmin) { highestPermission = "Admin"; }
            else if (isHeadStaff) { highestPermission = "Head Staff"; }
            else if (isSecurity) { highestPermission = "Security"; }
            else if (isStaff) { highestPermission = "Staff"; }
            else if (isDancer) { highestPermission = "Dancer"; }

            if (isAdmin || isHeadStaff || isSecurity)
            {
              // You could put something here to run if one has any of those roles.
            }

            hasAnyPermission = isAdmin || isHeadStaff || isSecurity || isStaff || isDancer;
            break; // Stop processing after finding the local user
          }
        }
        
        if (hasAnyPermission)
        {
          RunIfAnyPermissions();
        }
      }

      // Update the UI Text
      if (permissionDisplay != null)
      {
        string displayText =
            $"Username: {localUsername}\n" + $"Source: {permissionSource}\n" +
            $"Access Level: {highestPermission}";
        permissionDisplay.text = displayText;
      }
      else
      {
        Debug.LogError("permissionDisplay is not assigned!  Cannot display permission information.");
      }
    }

    //Helper function to process if the role is enabled
    bool IsRoleActive(string field)
    {
      return field.Trim() == "1";
    }

    void ProcessPatreonLists(string[] lines)
    {
      string[] patron1Array = new string[maxPatrons];
      string[] patron2Array = new string[maxPatrons];
      int patron1Count = 0;
      int patron2Count = 0;

      for (int i = 1; i < lines.Length; i++)
      {
        string line = lines[i];
        string[] fields = line.Split(',');

        if (fields.Length != 8)
        {
          continue;
        }

        string username = fields[0].Trim();
        bool isPatron1 = IsRoleActive(fields[6]);
        bool isPatron2 = IsRoleActive(fields[7]);

        if (isPatron1)
        {
          if (patron1Count < maxPatrons)
          {
            patron1Array[patron1Count] = username;
            patron1Count++;
          }
          else
          {
            Debug.LogWarning(
                "Too many Patron1 users! Increase maxPatrons to at least " +
                (patron1Count + 1));
          }
        }

        if (isPatron2)
        {
          if (patron2Count < maxPatrons)
          {
            patron2Array[patron2Count] = username;
            patron2Count++;
          }
          else
          {
            Debug.LogWarning(
                "Too many Patron2 users! Increase maxPatrons to at least " +
                (patron2Count + 1));
          }
        }
      }

      // Sort the arrays alphabetically using Bubble Sort
      BubbleSort(patron1Array, patron1Count);
      BubbleSort(patron2Array, patron2Count);

      // Build the strings from the sorted arrays
      string patron1String = "";
      for (int i = 0; i < patron1Count; i++)
      {
        patron1String += patron1Array[i] + "\n";
      }

      string patron2String = "";
      for (int i = 0; i < patron2Count; i++)
      {
        patron2String += patron2Array[i] + "\n";
      }

      if (patreonText1 != null) { patreonText1.text = patron1String; }
      else { Debug.LogError("patreonText1 is not assigned!"); }

      if (patreonText2 != null) { patreonText2.text = patron2String; }
      else { Debug.LogError("patreonText2 is not assigned!"); }
    }

    //Helper method to validate each row
    bool IsValidPermissionRow(string[] fields)
    {
      if (fields.Length != 8)
      {
        Debug.LogWarning("Invalid row length: " + fields.Length);
        return false;
      }

      if (string.IsNullOrEmpty(fields[0].Trim()))
      {
        Debug.LogWarning("Username is empty");
        return false;
      }

      return true;
    }

    void SetRoleActive(
        VRCPlayerApi player, GameObject[] roleObjects, string roleName,
        bool active)
    {
      Debug.Log($"Setting role: {roleName} to {(active ? "Active" : "Inactive")}");
      if (roleObjects == null || roleObjects.Length == 0)
      {
        Debug.LogWarning($"No GameObjects assigned for role: {roleName}");
        return;
      }

      foreach (GameObject obj in roleObjects)
      {
        if (obj != null)
        {
          Debug.Log($"Setting {obj.name} to {(active ? "Active" : "Inactive")} for role: {roleName}");
          obj.SetActive(active);
        }
        else
        {
          Debug.LogWarning($"Null GameObject in {roleName} array.");
        }
      }
    }

    //Helper Function to activate all roles on bypass
    void ApplyBypassPermissions(VRCPlayerApi player)
    {
      Debug.Log("Applying bypass permissions.");
      SetRoleActive(player, adminGameObjects, "Admin", true);
      SetRoleActive(player, headStaffGameObjects, "HeadStaff", true);
      SetRoleActive(player, securityGameObjects, "Security", true);
      SetRoleActive(player, staffGameObjects, "Staff", true);
      SetRoleActive(player, dancerGameObjects, "Dancer", true);
      RunIfAnyPermissions();
    }

    //Applies permissions to the defined functions
    void ApplyPermissions(
        VRCPlayerApi player, bool isAdmin, bool isHeadStaff, bool isSecurity,
        bool isStaff, bool isDancer)
    {
      if (isAdmin) { SetRoleActive(player, adminGameObjects, "Admin", true); }
      if (isHeadStaff) { SetRoleActive(player, headStaffGameObjects, "HeadStaff", true); }
      if (isSecurity) { SetRoleActive(player, securityGameObjects, "Security", true); }
      if (isStaff) { SetRoleActive(player, staffGameObjects, "Staff", true); }
      if (isDancer) { SetRoleActive(player, dancerGameObjects, "Dancer", true); }
    }

    private void SetRole(GameObject[] roleObjects)
    {
      SetRoleActive(Networking.LocalPlayer, roleObjects, roleObjects[0].name, true); //Assumes role name can be extracted from the first game object

      if (roleObjects == adminGameObjects || roleObjects == headStaffGameObjects || roleObjects == securityGameObjects) {
        // You could put something here to run if one has any of those roles.
      }
      UpdatePermissionDisplay("Keypad", GetHighestPermission());
    }

    public void SetAdmin()
    {
      SetRole(adminGameObjects);
      SetHeadStaff();
      SetSecurity();
      SetStaff();
    }

    public void SetHeadStaff() { SetRole(headStaffGameObjects); }

    public void SetSecurity() { SetRole(securityGameObjects); } //Additive role, won't work by itself.

    public void SetStaff() { SetRole(staffGameObjects); }

    public void SetDancer() { SetRole(dancerGameObjects); }

    // Event interface to de-auth a user.
    public void UndoAllPermissions()
    {
      VRCPlayerApi player = Networking.LocalPlayer;
      if (player == null)
      {
        Debug.LogError("Local player is null!");
        return;
      }

      ResetRole(player, adminGameObjects, "Admin");
      ResetRole(player, headStaffGameObjects, "HeadStaff");
      ResetRole(player, securityGameObjects, "Security");
      ResetRole(player, staffGameObjects, "Staff");
      ResetRole(player, dancerGameObjects, "Dancer");
    }

    void ResetRole(VRCPlayerApi player, GameObject[] roleObjects, string roleName)
    {
      SetRoleActive(player, roleObjects, roleName, false);
    }

    void RunIfAnyPermissions()
    {
      // You could put something here if a user has any permission.
    }

    //Helper function to determine what access level is being granted
    private string GetHighestPermission()
    {
      if (adminGameObjects[0].activeSelf) { return "Admin"; }
      if (headStaffGameObjects[0].activeSelf) { return "Head Staff"; }
      if (securityGameObjects[0].activeSelf) { return "Security"; }
      if (staffGameObjects[0].activeSelf) { return "Staff"; }
      if (dancerGameObjects[0].activeSelf) { return "Dancer"; }
      return "User";
    }

    //Helper function to set text field to display the current permission state.
    private void UpdatePermissionDisplay(string source, string accessLevel)
    {
      // Update the UI Text
      if (permissionDisplay != null)
      {
        string displayText =
            $"Username: {Networking.LocalPlayer.displayName}\n" +
            $"Source: {source}\n" + $"Access Level: {accessLevel}";
        permissionDisplay.text = displayText;
      }
      else
      {
        Debug.LogError("permissionDisplay is not assigned!");
      }
    }
    
    // Bubble Sort implementation
    void BubbleSort(string[] array, int count)
    {
      for (int i = 0; i < count - 1; i++)
      {
        var swapped = false;
        for (int j = 0; j < count - i - 1; j++)
        {
          if (String.CompareOrdinal(array[j], array[j + 1]) > 0)
          {
            // Swap elements
            string temp = array[j];
            array[j] = array[j + 1];
            array[j + 1] = temp;
            swapped = true;
          }
        }

        // If no two elements were swapped in inner loop, the array is sorted
        if (!swapped) { break; }
      }
    }
  }
}
