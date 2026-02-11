

CREATE TABLE "Rooms" (
                         "Id" text NOT NULL,
                         "Name" text NOT NULL,
                         "CreatedBy" text NOT NULL,
                         CONSTRAINT "PK_Rooms" PRIMARY KEY ("Id")
);


CREATE TABLE "SseConnections" (
                                  "ConnectionId" text NOT NULL,
                                  "ConnectedAt" timestamp with time zone NOT NULL,
                                  CONSTRAINT "PK_SseConnections" PRIMARY KEY ("ConnectionId")
);


CREATE TABLE "SseGroupMembers" (
                                   "ConnectionId" text NOT NULL,
                                   "GroupName" text NOT NULL,
                                   CONSTRAINT "PK_SseGroupMembers" PRIMARY KEY ("ConnectionId", "GroupName")
);


CREATE TABLE "Users" (
                         "Id" text NOT NULL,
                         "Nickname" text NOT NULL,
                         "Salt" text NOT NULL,
                         "Hash" text NOT NULL,
                         CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);


CREATE TABLE "Messages" (
                            "Id" text NOT NULL,
                            "Content" text NOT NULL,
                            "UserId" text NOT NULL,
                            "RoomId" text NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            CONSTRAINT "PK_Messages" PRIMARY KEY ("Id"),
                            CONSTRAINT "FK_Messages_Rooms_RoomId" FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_Messages_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "UserRooms" (
                             "UserId" text NOT NULL,
                             "RoomId" text NOT NULL,
                             CONSTRAINT "PK_UserRooms" PRIMARY KEY ("UserId", "RoomId"),
                             CONSTRAINT "FK_UserRooms_Rooms_RoomId" FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE CASCADE,
                             CONSTRAINT "FK_UserRooms_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_Messages_RoomId" ON "Messages" ("RoomId");


CREATE INDEX "IX_Messages_UserId" ON "Messages" ("UserId");


CREATE INDEX "IX_SseGroupMembers_GroupName" ON "SseGroupMembers" ("GroupName");


CREATE INDEX "IX_UserRooms_RoomId" ON "UserRooms" ("RoomId");