if COL_LENGTH('hm_messages', 'messageleaseowner') is null
   alter table hm_messages add messageleaseowner nvarchar(128) null

if COL_LENGTH('hm_messages', 'messageleaseexpiresutc') is null
   alter table hm_messages add messageleaseexpiresutc datetime2(3) null

if not exists (select * from sys.indexes where name = 'idx_hm_messages_delivery_lease' and object_id = object_id('hm_messages'))
   create index idx_hm_messages_delivery_lease
      on hm_messages (messagetype, messagelocked, messagenexttrytime, messageleaseexpiresutc)
      include (messagesize, messagecurnooftries, messageid)

if not exists (select * from sysobjects where id = object_id('hm_message_search_documents') and objectproperty(id, 'isusertable') = 1)
   create table hm_message_search_documents
   (
      messageid bigint not null,
      messageaccountid int not null,
      messagefolderid int not null,
      messageuid bigint not null,
      messageinternaldateutc datetime2(3) not null,
      messagesize bigint not null,
      messageflags tinyint not null,
      search_header nvarchar(max) not null,
      search_body nvarchar(max) not null,
      search_combined nvarchar(max) not null,
      updatedutc datetime2(3) not null,
      constraint pk_hm_message_search_documents primary key clustered (messageid)
   )

if not exists (select * from sys.indexes where name = 'idx_hm_message_search_documents_folder_uid' and object_id = object_id('hm_message_search_documents'))
   create index idx_hm_message_search_documents_folder_uid
      on hm_message_search_documents (messageaccountid, messagefolderid, messageuid)

if not exists (select * from sysobjects where id = object_id('hm_message_search_queue') and objectproperty(id, 'isusertable') = 1)
   create table hm_message_search_queue
   (
      messageid bigint not null,
      queuedutc datetime2(3) not null,
      attempts int not null,
      lastattemptutc datetime2(3) null,
      nextattemptutc datetime2(3) null,
      searchleaseowner nvarchar(128) null,
      searchleaseexpiresutc datetime2(3) null,
      lasterror nvarchar(1024) null,
      constraint pk_hm_message_search_queue primary key clustered (messageid)
   )

if COL_LENGTH('hm_message_search_queue', 'nextattemptutc') is null
   alter table hm_message_search_queue add nextattemptutc datetime2(3) null

if COL_LENGTH('hm_message_search_queue', 'searchleaseowner') is null
   alter table hm_message_search_queue add searchleaseowner nvarchar(128) null

if COL_LENGTH('hm_message_search_queue', 'searchleaseexpiresutc') is null
   alter table hm_message_search_queue add searchleaseexpiresutc datetime2(3) null

if not exists (select * from sys.indexes where name = 'idx_hm_message_search_queue_lease' and object_id = object_id('hm_message_search_queue'))
   create index idx_hm_message_search_queue_lease
      on hm_message_search_queue (nextattemptutc, searchleaseexpiresutc, attempts, queuedutc)
      include (messageid, searchleaseowner)

if not exists (select * from sys.fulltext_catalogs where name = 'hm_message_search_catalog')
   create fulltext catalog hm_message_search_catalog as default

if not exists (select * from sys.fulltext_indexes where object_id = object_id('hm_message_search_documents'))
   create fulltext index on hm_message_search_documents
   (
      search_header language 0x0,
      search_body language 0x0,
      search_combined language 0x0
   )
   key index pk_hm_message_search_documents
   on hm_message_search_catalog
   with change_tracking auto

update hm_dbversion set value = 6000
