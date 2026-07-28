<?php

   if (!defined('IN_WEBADMIN'))
      exit();

   if (hmailGetAdminLevel() != 2)
   	  hmailHackingAttemp(); // Domain admin but not for this domain.

   hmailRequirePostCsrfToken();

   $obSettings = $obBaseApp->Settings;
   $obRoutes	= $obSettings->Routes;
   
   $routeid 	= hmailGetPostVar("routeid",0);
   $routeaddressid	= hmailGetPostVar("routeaddressid",0);
   $action	   = hmailGetPostVar("action","");

   $obRoute       = $obRoutes->ItemByDBID($routeid);
   $obAddresses	= $obRoute->Addresses;
   
   $routeaddress = hmailGetPostVar("routeaddress","");
   
   if ($action == "edit")
      $obAddress = $obAddresses->ItemByDBID($routeaddressid);
   elseif ($action == "add")
      $obAddress = $obAddresses->Add();
   elseif ($action == "delete")
   {
      $obAddresses->DeleteByDBID($routeaddressid);
      header("Location: index.php?page=route_addresses&routeid=$routeid");
      exit();
   }

   $obAddress->Address = $routeaddress;
   $obAddress->RouteID = $routeid;
         
   $obAddress->Save();
   
   header("Location: index.php?page=route_addresses&routeid=$routeid");

?>

