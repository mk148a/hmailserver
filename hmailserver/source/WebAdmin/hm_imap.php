<?php
if (!defined('IN_WEBADMIN'))
   exit();

if (hmailGetAdminLevel() != 2)
	hmailHackingAttemp();

$obSettings	= $obBaseApp->Settings();

$action	   = hmailGetPostVar("action","");

if($action == "save")
{
	hmailRequirePostCsrfToken();
	$obSettings->WelcomeIMAP= hmailGetPostVar("welcomeimap",0);
	$obSettings->MaxIMAPConnections = hmailGetPostVar("MaxIMAPConnections",0);
	
	$obSettings->IMAPSortEnabled  = hmailGetPostVar("IMAPSortEnabled",0);
	$obSettings->IMAPQuotaEnabled = hmailGetPostVar("IMAPQuotaEnabled",0);
	$obSettings->IMAPIdleEnabled  = hmailGetPostVar("IMAPIdleEnabled",0);
	$obSettings->IMAPACLEnabled  = hmailGetPostVar("IMAPACLEnabled",0);
    
    $obSettings->IMAPSASLPlainEnabled  = hmailGetPostVar("IMAPSASLPlainEnabled",0);
    $obSettings->IMAPSASLInitialResponseEnabled  = hmailGetPostVar("IMAPSASLInitialResponseEnabled",0);
    $obSettings->IMAPMasterUser  = hmailGetPostVar("IMAPMasterUser","");
   
    $obSettings->IMAPHierarchyDelimiter = hmailGetPostVar("IMAPHierarchyDelimiter","");
}

$welcomeimap = $obSettings->WelcomeIMAP;     
$MaxIMAPConnections = $obSettings->MaxIMAPConnections;

$IMAPSortEnabled  = $obSettings->IMAPSortEnabled;
$IMAPQuotaEnabled = $obSettings->IMAPQuotaEnabled;
$IMAPIdleEnabled  = $obSettings->IMAPIdleEnabled;
$IMAPACLEnabled  = $obSettings->IMAPACLEnabled;


$IMAPSASLPlainEnabled  = $obSettings->IMAPSASLPlainEnabled;
$IMAPSASLInitialResponseEnabled  = $obSettings->IMAPSASLInitialResponseEnabled;
$IMAPMasterUser  = $obSettings->IMAPMasterUser;

$IMAPHierarchyDelimiter = $obSettings->IMAPHierarchyDelimiter;

?>

<h1><?php EchoTranslation("IMAP")?></h1>

<form action="index.php" method="post" onSubmit="return formCheck(this);">
   <?php
      PrintHiddenCsrfToken();
      PrintHidden("page", "imap");
      PrintHidden("action", "save");
   ?>   
   
   <div class="tabber">
      <div class="tabbertab">
         <h2><?php EchoTranslation("General")?></h2>       

      	<table border="0" width="100%" cellpadding="5">
            <tr>
               <th width="30%"></th>
               <th width="70%"></th>
            </tr>   
            <?php
               PrintPropertyEditRow("MaxIMAPConnections", "Maximum number of simultaneous connections (0 for unlimited)", $MaxIMAPConnections, 50);
               PrintPropertyEditRow("welcomeimap", "Welcome message", $welcomeimap, 50);
            ?>
       	</table>
      </div>
      <div class="tabbertab">
         <h2><?php EchoTranslation("Advanced")?></h2>      
	
       	<table border="0" width="100%" cellpadding="5">
            <tr>
               <td width="30%">
               </td>
               <td  width="70%">
               </td>
            </tr>
            <?php
               PrintCheckboxRow("IMAPSortEnabled", "Sort", $IMAPSortEnabled);
               PrintCheckboxRow("IMAPQuotaEnabled", "Quota", $IMAPQuotaEnabled);
               PrintCheckboxRow("IMAPIdleEnabled", "Idle", $IMAPIdleEnabled);
               PrintCheckboxRow("IMAPACLEnabled", "ACL", $IMAPACLEnabled);
               
               PrintCheckboxRow("IMAPSASLPlainEnabled", "SASL Plain", $IMAPSASLPlainEnabled);
               PrintCheckboxRow("IMAPSASLInitialResponseEnabled", "SASL Initial Client Response", $IMAPSASLInitialResponseEnabled);
               PrintPropertyEditRow("IMAPMasterUser", "IMAP Master user", $IMAPMasterUser);
            ?>
            
      		<tr>
      			<td><?php EchoTranslation("Hierarchy delimiter")?></td>
      			<td>
      				<select name="IMAPHierarchyDelimiter" style="font-family: Trebuchet MS, Verdana, Arial, Helvetica, sans-serif">
      					<option value="." <?php if ($IMAPHierarchyDelimiter == ".") echo "selected";?> >.</option>
      					<option value="\" <?php if ($IMAPHierarchyDelimiter == "\\") echo "selected";?> >\</option>
      					<option value="/" <?php if ($IMAPHierarchyDelimiter == "/") echo "selected";?> >/</option>
      				</select>
      		
      			</td>
      		</tr>              
      	</table>
      </div>
   </div>
   <?php
      PrintSaveButton();
   ?>
</form>
