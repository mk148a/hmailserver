<?php
if (!defined('IN_WEBADMIN'))
   exit();

if (hmailGetAdminLevel() != 2)
	hmailHackingAttemp(); // Users are not allowed to show this page.

?>
<h1><?php EchoTranslation("Addresses")?></h1>
<table border="0" width="100%" cellpadding="5">
<?php

$routeid	= hmailGetVar("routeid",0);

$bgcolor = "#EEEEEE";

$obRoutes	= $obSettings->Routes();
$obRoute    = $obRoutes->ItemByDBID($routeid);
$obAddresses = $obRoute->Addresses();

$Count = $obAddresses->Count();

$str_delete = $obLanguage->String("Remove");

for ($i = 0; $i < $Count; $i++)
{
	$obAddress           = $obAddresses->Item($i);
	$routeaddress        = PreprocessOutput($obAddress->Address);
	$routeaddressid      = $obAddress->ID;
	
	echo "<tr bgcolor=\"$bgcolor\">";
	echo "<td width=\"80%\"><a href=\"?page=route_address&action=edit&routeid=$routeid&routeaddressid=$routeaddressid\">$routeaddress</a></td>";
	echo "<td width=\"20%\">";
	echo "<form action=\"index.php\" method=\"post\" style=\"display:inline\">";
	PrintHiddenCsrfToken();
	PrintHidden("page", "background_route_address_save");
	PrintHidden("action", "delete");
	PrintHidden("routeid", $routeid);
	PrintHidden("routeaddressid", $routeaddressid);
	echo "<input type=\"submit\" value=\"$str_delete\">";
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
      <a href="?page=route_address&action=add&routeid=<?php echo $routeid?>"><i><?php EchoTranslation("Add")?></i></a>
   </td>
</tr>

</table>

