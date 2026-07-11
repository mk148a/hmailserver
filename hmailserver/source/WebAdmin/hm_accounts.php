<?php
if (!defined('IN_WEBADMIN'))
   exit();
?>

<h1><?php EchoTranslation("Accounts")?></h1>

<?php

$domainid  = hmailGetVar("domainid",null, true);

if (hmailGetAdminLevel() == 0)
	hmailHackingAttemp(); // Users are not allowed to show this page.

if (hmailGetAdminLevel() == 1 && $domainid != hmailGetDomainID())
	hmailHackingAttemp(); // Domain admin but not for this domain.

echo '<table border="0" width="100%" cellpadding="5">';

$bgcolor = "#EEEEEE";

$obDomain				= $obBaseApp->Domains->ItemByDBID($domainid);
$obAccounts 			= $obDomain->Accounts();

$Count = $obAccounts->Count();

$str_delete = $obLanguage->String("Remove");

$currentaccountid = hmailGetAccountID();

$str_accountaddress  = $obLanguage->String("Address");
$str_maxsizemb       = $obLanguage->String("Maximum size (MB)");

echo "<tr bgcolor=\"#CCCCCC\">";
echo "<td width=\"60%\">$str_accountaddress</td>";
echo "<td width=\"20%\">$str_maxsizemb</td>";
echo "<td width=\"20%\"></td>";
echo "</tr>";

$obAccounts = $obDomain->Accounts;

for ($i = 0; $i < $Count; $i++)
{
	$obAccount           = $obAccounts->Item($i);
	$accountaddress      = $obAccount->Address;
	$accountid           = $obAccount->ID;
	$accountmaxsize      = $obAccount->MaxSize();
	
   $accountaddress = PreprocessOutput($accountaddress);
   
	echo "<tr bgcolor=\"$bgcolor\">";
	echo "<td width=\"60%\"><a href=\"?page=account&action=edit&domainid=$domainid&accountid=$accountid\">$accountaddress</a></td>";
	echo "<td width=\"20%\">$accountmaxsize</td>";
	
	echo "<td width=\"20%\">";
	if ($currentaccountid != $accountid)
   {
      $confirm_delete = str_replace("%s", EscapeStringForJs($accountaddress), GetConfirmDelete());
      echo "<form action=\"index.php\" method=\"post\" style=\"display:inline\" onsubmit=\"return confirm('$confirm_delete');\">";
      PrintHiddenCsrfToken();
      PrintHidden("page", "background_account_save");
      PrintHidden("action", "delete");
      PrintHidden("domainid", $domainid);
      PrintHidden("accountid", $accountid);
      echo "<input type=\"submit\" value=\"$str_delete\">";
      echo "</form>";
   }

	else "</td>";
	   
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
      <a href="?page=account&action=add&domainid=<?php echo $domainid?>"><i><?php EchoTranslation("Add")?></i></a>
   </td>
</tr>
</table>
