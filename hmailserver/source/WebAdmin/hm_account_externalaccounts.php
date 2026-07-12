<?php
if (!defined('IN_WEBADMIN'))
   exit();

$domainid	= hmailGetVar("domainid",0,true);
$accountid	= hmailGetVar("accountid",0,true);
$action	   = hmailGetVar("action","");

if (hmailGetAdminLevel() == 0 && ($accountid != hmailGetAccountID() || $domainid != hmailGetDomainID()))
   hmailHackingAttemp();

if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())
	hmailHackingAttemp(); // Domain admin but not for this domain.

$obDomain	= $obBaseApp->Domains->ItemByDBID($domainid);
$obAccount = $obDomain->Accounts->ItemByDBID($accountid);  
$obFetchAccounts = $obAccount->FetchAccounts();

$action	   = hmailGetVar("action","");

?>

<h1><?php EchoTranslation("External accounts")?></h1>

<table border="0" width="100%" cellpadding="5">
	<tr>
		<td width="30%"><?php EchoTranslation("Name")?></td>
		<td width="20%"><?php EchoTranslation("Server address")?></td>
		<td width="20%"></td>
		<td width="20%"></td>
	</tr>
	
<?php
$bgcolor = "#EEEEEE";

$Count = $obFetchAccounts->Count();


$str_delete = $obLanguage->String("Remove");
$str_downloadnow = $obLanguage->String("Download now");

for ($i = 0; $i < $Count; $i++)
{
	$obFetchAccount          = $obFetchAccounts->Item($i);
	
	$FAID   				 = $obFetchAccount->ID;
	$Name  					 = PreprocessOutput($obFetchAccount->Name);
	$ServerAddress			 = PreprocessOutput($obFetchAccount->ServerAddress);
	
	
	echo "<tr bgcolor=\"$bgcolor\">";
	echo "<td><a href=\"?page=account_externalaccount&action=edit&domainid=$domainid&accountid=$accountid&faid=$FAID\">$Name</a></td>";
	echo "<td><a href=\"?page=account_externalaccount&action=edit&domainid=$domainid&accountid=$accountid&faid=$FAID\">$ServerAddress</a></td>";
	echo "<td>";
	echo "<form action=\"index.php\" method=\"post\" style=\"display:inline\">";
	PrintHiddenCsrfToken();
	PrintHidden("page", "background_account_externalaccount_save");
	PrintHidden("action", "delete");
	PrintHidden("domainid", $domainid);
	PrintHidden("accountid", $accountid);
	PrintHidden("faid", $FAID);
	echo "<input type=\"submit\" value=\"$str_delete\">";
	echo "</form>";
	echo "</td>";
	echo "<td>";
	echo "<form action=\"index.php\" method=\"post\" style=\"display:inline\">";
	PrintHiddenCsrfToken();
	PrintHidden("page", "background_account_externalaccount_save");
	PrintHidden("action", "downloadnow");
	PrintHidden("domainid", $domainid);
	PrintHidden("accountid", $accountid);
	PrintHidden("faid", $FAID);
	echo "<input type=\"submit\" value=\"$str_downloadnow\">";
	echo "</form>";
	echo "</td>";
	echo "</tr>";
	
	if ($bgcolor == "#EEEEEE")
	   $bgcolor = "#DDDDDD";
	else
	   $bgcolor = "#EEEEEE";
}
?>
<tr>
   <td>
      <br>
      <a href="?page=account_externalaccount&action=add&domainid=<?php echo $domainid?>&accountid=<?php echo $accountid?>"><i><?php EchoTranslation("Add")?></i></a>
   </td>
</tr>
</table>
