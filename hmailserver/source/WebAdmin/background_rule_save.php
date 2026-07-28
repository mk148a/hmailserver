<?php

   if (!defined('IN_WEBADMIN'))
      exit();

   hmailRequirePostCsrfToken();

   $action	   = hmailGetPostVar("action","");
   $domainid   = hmailGetPostVar("domainid", 0, true);
   $accountid  = hmailGetPostVar("accountid", 0, true);
   $ruleid     = hmailGetPostVar("ruleid", 0);
   $criteriaid = hmailGetPostVar("criteriaid", 0);
   $actionid   = hmailGetPostVar("actionid", 0);
   $savetype   = hmailGetPostVar("savetype", 0);
      
   if (!GetHasRuleAccess($domainid, $accountid))
   	hmailHackingAttemp();

   include "include/rule_strings.php";
      
   $rule_link = "index.php?page=rule&action=edit&domainid=$domainid&accountid=$accountid&ruleid=$ruleid";

   if ($action == "add" && $savetype == "rule")
   {
      if ($domainid == 0)
         $rule = $obBaseApp->Rules->Add();
      else
         $rule = $obBaseApp->Domains->ItemByDBID($domainid)->Accounts->ItemByDBID($accountid)->Rules->Add();
   }
   else
   {
      if ($domainid == 0)
         $rule = $obBaseApp->Rules->ItemByDBID($ruleid);
      else
         $rule = $obBaseApp->Domains->ItemByDBID($domainid)->Accounts->ItemByDBID($accountid)->Rules->ItemByDBID($ruleid);
   }  
 

   if ($action == "delete")
   {
   
      if ($savetype == "criteria")
         $rule->Criterias->ItemByDBID($criteriaid)->Delete();
      else if ($savetype == "action")
         $rule->Actions->ItemByDBID($actionid)->Delete();
      else if ($savetype == "rule")
         $rule->Delete();
      
      if ($savetype == "criteria" || $savetype == "action")
         header("Location: $rule_link");
      else
      {
         if ($domainid == 0)
            header("Location: index.php?page=rules");
         else
            header("Location: index.php?page=account&action=edit&accountid=$accountid&domainid=$domainid");
      }
         
      die;
   }
   
   if ($savetype == "criteria")
   {
      
      if ($action == "edit")
         $criteria = $rule->Criterias->ItemByDBID($criteriaid);
      else if ($action == "add")
      {
         $criteria = $rule->Criterias->Add();
      }
   
      $criteria->UsePredefined = hmailGetPostVar("UsePredefined", 0);
      $criteria->PredefinedField = hmailGetPostVar("PredefinedField", 0);
      $criteria->MatchType = hmailGetPostVar("MatchType", 0);
      $criteria->MatchValue = hmailGetPostVar("MatchValue", 0);
      $criteria->HeaderField = hmailGetPostVar("HeaderField", 0);
      
      $criteria->Save();
      
      $rule->Save();
    
      header("Location: $rule_link");
      die;
   }
   else if ($savetype == "action")
   {
   
      if ($action == "edit")
         $actionObj = $rule->Actions->ItemByDBID($actionid);
      else if ($action == "add")
         $actionObj = $rule->Actions->Add();
   
      $type = hmailGetPostVar("Type", 0);
      
      if (hmailGetAdminLevel() != ADMIN_SERVER)
      {
         if ($type != eRADeleteEmail && 
             $type != eRAForwardEmail &&
             $type != eRAReply &&
             $type != eRAMoveToImapFolder &&
             $type != eRAStopRuleProcessing &&
             $type != eRASetHeaderValue)
         {
            hmailHackingAttemp();
         }  
      }
   
      $actionObj->To = hmailGetPostVar("To", "");
      $actionObj->IMAPFolder = hmailGetPostVar("IMAPFolder", "");
      $actionObj->ScriptFunction = hmailGetPostVar("ScriptFunction", "");
      $actionObj->FromName = hmailGetPostVar("FromName", "");
      $actionObj->FromAddress = hmailGetPostVar("FromAddress", "");
      $actionObj->Subject = hmailGetPostVar("Subject", "");
      $actionObj->Body = hmailGetPostVar("Body", "");
      $actionObj->HeaderName = hmailGetPostVar("HeaderName", "");
      
      $replyabortspamflagged = hmailGetPostVar("replyabortspamflagged", "0");
      $forwardabortspamflagged = hmailGetPostVar("forwardabortspamflagged", "0");
      
	  switch ($type)
	  {
		case eRASetHeaderValue:
			$actionObj->Value = hmailGetPostVar("Value", "");
			break;
		case eRABindToAddress:
			$actionObj->Value = hmailGetPostVar("BindToAddress", "");
			break;
		case eRAForwardEmail:
			$actionObj->AbortSpamFlagged = $forwardabortspamflagged == 1;
			break;
		case eRAReply:
			$actionObj->AbortSpamFlagged = $replyabortspamflagged == 1;
			break;
	  }
      
	  $actionObj->Type = $type;

      $actionObj->Save();
      
      $rule->Save();
      
      header("Location: $rule_link");   
      die;
   }
   else if ($savetype == "rule")
   {
      $rule->Name = hmailGetPostVar("Name", "");
      $rule->Active = hmailGetPostVar("Active", "") == "1";
      $rule->UseAND = hmailGetPostVar("UseAND", "") == "1";
      $rule->Save();
      
      $ruleid = $rule->ID;
      
      // can't re-use rule_link since the rule id might be new (if add)
      header("Location: index.php?page=rule&action=edit&domainid=$domainid&accountid=$accountid&ruleid=$ruleid");   
      die;
   }

   
?>

