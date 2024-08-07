﻿Imports Mirage.Sharp.Asfw
Imports Core

Module S_Npc

#Region "Spawning"

    Sub SpawnAllMapNpcs()
        Dim i As Integer

        For i = 1 To MAX_MAPS
            SpawnMapNpcs(i)
        Next

    End Sub

    Sub SpawnMapNpcs(mapNum As Integer)
        Dim i As Integer

        For i = 1 To MAX_MAP_NPCS
            SpawnNpc(i, mapNum)
        Next

    End Sub

    Friend Sub SpawnNpc(mapNpcNum As Integer, mapNum As Integer)
        Dim buffer As New ByteStream(4)
        Dim npcNum As Integer
        Dim x As Integer
        Dim y As Integer
        Dim i = 0
        Dim spawned As Boolean

        If Map(mapNum).NoRespawn Then Exit Sub

        npcNum = Map(mapNum).Npc(mapNpcNum)

        If npcNum > 0 Then
            If Not NPC(npcNum).SpawnTime = Core.Time.Instance.TimeOfDay And NPC(npcNum).SpawnTime <> 0 Then
                ClearMapNpc(mapNpcNum, mapNum)
                SendMapNpcsToMap(mapNum)
                Exit Sub
            End If

            MapNPC(mapNum).Npc(mapNpcNum).Num = npcNum
            MapNPC(mapNum).Npc(mapNpcNum).Target = 0
            MapNPC(mapNum).Npc(mapNpcNum).TargetType = 0 ' clear

            For i = 1 To VitalType.Count - 1
                MapNPC(mapNum).Npc(mapNpcNum).Vital(i) = GetNpcMaxVital(npcNum, i)
            Next

            MapNPC(mapNum).Npc(mapNpcNum).Dir = Int(Rnd() * 4)

            'Check if theres a spawn tile for the specific npc
            For x = 0 To Map(mapNum).MaxX
                For y = 0 To Map(mapNum).MaxY
                    If Map(mapNum).Tile(x, y).Type = TileType.NpcSpawn Then
                        If Map(mapNum).Tile(x, y).Data1 = mapNpcNum Then
                            MapNPC(mapNum).Npc(mapNpcNum).X = x
                            MapNPC(mapNum).Npc(mapNpcNum).Y = y
                            MapNPC(mapNum).Npc(mapNpcNum).Dir = Map(mapNum).Tile(x, y).Data2
                            spawned = True
                            Exit For
                        End If
                    End If
                Next y
            Next x

            If Not spawned Then
                ' Well try 100 times to randomly place the sprite
                While i < 1000
                    x = Random(0, Map(mapNum).MaxX)
                    y = Random(0, Map(mapNum).MaxY)

                    If x > Map(mapNum).MaxX Then x = Map(mapNum).MaxX
                    If y > Map(mapNum).MaxY Then y = Map(mapNum).MaxY

                    ' Check if the tile is walkable
                    If NpcTileIsOpen(mapNum, x, y) Then
                        MapNPC(mapNum).Npc(mapNpcNum).X = x
                        MapNPC(mapNum).Npc(mapNpcNum).Y = y
                        spawned = True
                        Exit While
                    End If
                    i += 1
                End While
            End If

            ' Didn't spawn, so now we'll just try to find a free tile
            If Not spawned Then
                For x = 0 To Map(mapNum).MaxX
                    For y = 0 To Map(mapNum).MaxY
                        If NpcTileIsOpen(mapNum, x, y) Then
                            MapNPC(mapNum).Npc(mapNpcNum).X = x
                            MapNPC(mapNum).Npc(mapNpcNum).Y = y
                            spawned = True
                        End If
                    Next
                Next
            End If

            ' If we suceeded in spawning then send it to everyone
            If spawned Then
                buffer.WriteInt32(ServerPackets.SSpawnNpc)
                buffer.WriteInt32(mapNpcNum)
                buffer.WriteInt32(MapNPC(mapNum).Npc(mapNpcNum).Num)
                buffer.WriteInt32(MapNPC(mapNum).Npc(mapNpcNum).X)
                buffer.WriteInt32(MapNPC(mapNum).Npc(mapNpcNum).Y)
                buffer.WriteInt32(MapNPC(mapNum).Npc(mapNpcNum).Dir)
                
                For i = 1 To VitalType.Count - 1
                    buffer.WriteInt32(MapNPC(mapNum).Npc(mapNpcNum).Vital(i))
                Next

                SendDataToMap(mapNum, buffer.Data, buffer.Head)
            End If

            SendMapNpcVitals(mapNum, mapNpcNum)
        End If

        buffer.Dispose()
    End Sub

#End Region

#Region "Movement"

    Friend Function NpcTileIsOpen(mapNum As Integer, x As Integer, y As Integer) As Boolean
        Dim i As Integer
        NpcTileIsOpen = True

        If PlayersOnMap(mapNum) Then
            For i = 1 To Socket.HighIndex
                If GetPlayerMap(i) = mapNum And GetPlayerX(i) = x And GetPlayerY(i) = y Then
                    NpcTileIsOpen = False
                    Exit Function
                End If
            Next
        End If

        For LoopI = 1 To MAX_MAP_NPCS
            If MapNPC(mapNum).Npc(LoopI).Num > 0 And MapNPC(mapNum).Npc(LoopI).X = x And MapNPC(mapNum).Npc(LoopI).Y = y Then
                NpcTileIsOpen = False
                Exit Function
            End If
        Next

        If Map(mapNum).Tile(x, y).Type <> TileType.NpcSpawn And Map(mapNum).Tile(x, y).Type <> TileType.Item And Map(mapNum).Tile(x, y).Type <> TileType.None And Map(mapNum).Tile(x, y).Type2 <> TileType.NpcSpawn And Map(mapNum).Tile(x, y).Type2 <> TileType.Item And Map(mapNum).Tile(x, y).Type2 <> TileType.None Then
            NpcTileIsOpen = False
        End If

    End Function

    Function CanNpcMove(mapNum As Integer, MapNpcNum As Integer, Dir As Byte) As Boolean
        Dim i As Integer
        Dim n As Integer, n2 As Integer
        Dim x As Integer
        Dim y As Integer

        ' Check for subscript out of range
        If mapNum <= 0 Or mapNum > MAX_MAPS Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Dir <= DirectionType.None Or Dir > DirectionType.Left Then
            Exit Function
        End If

        x = MapNPC(mapNum).Npc(MapNpcNum).X
        y = MapNPC(mapNum).Npc(MapNpcNum).Y
        CanNpcMove = True

        Select Case Dir
            Case DirectionType.Up
                ' Check to make sure not outside of boundaries
                If y > 0 Then
                    n = Map(mapNum).Tile(x, y - 1).Type
                    n2 = Map(mapNum).Tile(x, y - 1).Type2

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn And n2 <> TileType.None And n2 <> TileType.Item And n2 <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To Socket.HighIndex()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = mapNum) And (GetPlayerX(i) = MapNPC(mapNum).Npc(MapNpcNum).X) And (GetPlayerY(i) = MapNPC(mapNum).Npc(MapNpcNum).Y - 1) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNPC(mapNum).Npc(i).Num > 0) And (MapNPC(mapNum).Npc(i).X = MapNPC(mapNum).Npc(MapNpcNum).X) And (MapNPC(mapNum).Npc(i).Y = MapNPC(mapNum).Npc(MapNpcNum).Y - 1) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

            Case DirectionType.Down
                ' Check to make sure not outside of boundaries
                If y < Map(mapNum).MaxY Then
                    n = Map(mapNum).Tile(x, y + 1).Type
                    n2 = Map(mapNum).Tile(x, y + 1).Type2

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn And n2 <> TileType.None And n2 <> TileType.Item And n2 <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To Socket.HighIndex()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = mapNum) And (GetPlayerX(i) = MapNPC(mapNum).Npc(MapNpcNum).X) And (GetPlayerY(i) = MapNPC(mapNum).Npc(MapNpcNum).Y + 1) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNPC(mapNum).Npc(i).Num > 0) And (MapNPC(mapNum).Npc(i).X = MapNPC(mapNum).Npc(MapNpcNum).X) And (MapNPC(mapNum).Npc(i).Y = MapNPC(mapNum).Npc(MapNpcNum).Y + 1) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

            Case DirectionType.Left
                ' Check to make sure not outside of boundaries
                If x > 0 Then
                    n = Map(mapNum).Tile(x - 1, y).Type
                    n2 = Map(mapNum).Tile(x - 1, y).Type2

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn And n2 <> TileType.None And n2 <> TileType.Item And n2 <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To Socket.HighIndex()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = mapNum) And (GetPlayerX(i) = MapNPC(mapNum).Npc(MapNpcNum).X - 1) And (GetPlayerY(i) = MapNPC(mapNum).Npc(MapNpcNum).Y) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNPC(mapNum).Npc(i).Num > 0) And (MapNPC(mapNum).Npc(i).X = MapNPC(mapNum).Npc(MapNpcNum).X - 1) And (MapNPC(mapNum).Npc(i).Y = MapNPC(mapNum).Npc(MapNpcNum).Y) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

            Case DirectionType.Right
                ' Check to make sure not outside of boundaries
                If x < Map(mapNum).MaxX Then
                    n = Map(mapNum).Tile(x + 1, y).Type
                    n2 = Map(mapNum).Tile(x + 1, y).Type2

                    ' Check to make sure that the tile is walkable
                    If n <> TileType.None And n <> TileType.Item And n <> TileType.NpcSpawn And n2 <> TileType.None And n2 <> TileType.Item And n2 <> TileType.NpcSpawn Then
                        CanNpcMove = False
                        Exit Function
                    End If

                    ' Check to make sure that there is not a player in the way
                    For i = 1 To Socket.HighIndex()
                        If IsPlaying(i) Then
                            If (GetPlayerMap(i) = mapNum) And (GetPlayerX(i) = MapNPC(mapNum).Npc(MapNpcNum).X + 1) And (GetPlayerY(i) = MapNPC(mapNum).Npc(MapNpcNum).Y) Then
                                CanNpcMove = False
                                Exit Function
                            End If
                        End If
                    Next

                    ' Check to make sure that there is not another npc in the way
                    For i = 1 To MAX_MAP_NPCS
                        If (i <> MapNpcNum) And (MapNPC(mapNum).Npc(i).Num > 0) And (MapNPC(mapNum).Npc(i).X = MapNPC(mapNum).Npc(MapNpcNum).X + 1) And (MapNPC(mapNum).Npc(i).Y = MapNPC(mapNum).Npc(MapNpcNum).Y) Then
                            CanNpcMove = False
                            Exit Function
                        End If
                    Next
                Else
                    CanNpcMove = False
                End If

        End Select

        If MapNPC(mapNum).Npc(MapNpcNum).SkillBuffer > 0 Then CanNpcMove = False

    End Function

    Sub NpcMove(mapNum As Integer, MapNpcNum As Integer, Dir As Integer, Movement As Integer)
        Dim buffer As New ByteStream(4)

        ' Check for subscript out of range
        If mapNum <= 0 Or mapNum > MAX_MAPS Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Dir <= DirectionType.None Or Dir > DirectionType.Left Or Movement < 0 Or Movement > 2 Then
            Exit Sub
        End If

        MapNPC(mapNum).Npc(MapNpcNum).Dir = Dir

        Select Case Dir
            Case DirectionType.Up
                MapNPC(mapNum).Npc(MapNpcNum).Y = MapNPC(mapNum).Npc(MapNpcNum).Y - 1

                buffer.WriteInt32(ServerPackets.SNpcMove)
                buffer.WriteInt32(MapNpcNum)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).X)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Y)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Dir)
                buffer.WriteInt32(Movement)

                SendDataToMap(mapNum, buffer.Data, buffer.Head)
            Case DirectionType.Down
                MapNPC(mapNum).Npc(MapNpcNum).Y = MapNPC(mapNum).Npc(MapNpcNum).Y + 1

                buffer.WriteInt32(ServerPackets.SNpcMove)
                buffer.WriteInt32(MapNpcNum)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).X)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Y)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Dir)
                buffer.WriteInt32(Movement)

                SendDataToMap(mapNum, buffer.Data, buffer.Head)
            Case DirectionType.Left
                MapNPC(mapNum).Npc(MapNpcNum).X = MapNPC(mapNum).Npc(MapNpcNum).X - 1

                buffer.WriteInt32(ServerPackets.SNpcMove)
                buffer.WriteInt32(MapNpcNum)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).X)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Y)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Dir)
                buffer.WriteInt32(Movement)

                SendDataToMap(mapNum, buffer.Data, buffer.Head)
            Case DirectionType.Right
                MapNPC(mapNum).Npc(MapNpcNum).X = MapNPC(mapNum).Npc(MapNpcNum).X + 1

                buffer.WriteInt32(ServerPackets.SNpcMove)
                buffer.WriteInt32(MapNpcNum)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).X)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Y)
                buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Dir)
                buffer.WriteInt32(Movement)

                SendDataToMap(mapNum, buffer.Data, buffer.Head)
        End Select

        buffer.Dispose()
    End Sub

    Sub NpcDir(mapNum As Integer, MapNpcNum As Integer, Dir As Integer)
        Dim buffer As New ByteStream(4)

        ' Check for subscript out of range
        If mapNum <= 0 Or mapNum > MAX_MAPS Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Dir <= DirectionType.None Or Dir > DirectionType.Left Then
            Exit Sub
        End If

        MapNPC(mapNum).Npc(MapNpcNum).Dir = Dir

        buffer.WriteInt32(ServerPackets.SNpcDir)
        buffer.WriteInt32(MapNpcNum)
        buffer.WriteInt32(Dir)

        SendDataToMap(mapNum, buffer.Data, buffer.Head)

        buffer.Dispose()
    End Sub

#End Region

#Region "Npcombat"

    Friend Sub TryNpcAttackPlayer(mapnpcnum As Integer, index As Integer)

        Dim mapNum As Integer, npcnum As Integer, Damage As Integer, i As Integer, armor As Integer

        ' Can the npc attack the player?
        If CanNpcAttackPlayer(mapnpcnum, index) Then
            mapNum = GetPlayerMap(index)
            npcnum = MapNPC(mapNum).Npc(mapnpcnum).Num

            ' check if PLAYER can avoid the attack
            If CanPlayerDodge(index) Then
                SendActionMsg(mapNum, "Dodge!", ColorType.Pink, 1, (Player(index).X * 32), (Player(index).Y * 32))
                Exit Sub
            End If

            If CanPlayerParry(index) Then
                SendActionMsg(mapNum, "Parry!", ColorType.Pink, 1, (Player(index).X * 32), (Player(index).Y * 32))
                Exit Sub
            End If

            ' Get the damage we can do
            Damage = GetNpcDamage(npcnum)

            If CanPlayerBlockHit(index) Then
                SendActionMsg(mapNum, "Block!", ColorType.Pink, 1, (Player(index).X * 32), (Player(index).Y * 32))
                Exit Sub
            Else

                For i = 2 To EquipmentType.Count - 1 ' start at 2, so we skip weapon
                    If GetPlayerEquipment(index, i) > 0 Then
                        armor = armor + Item(GetPlayerEquipment(index, i)).Data2
                    End If
                Next
                ' take away armour
                Damage = Damage - ((GetPlayerStat(index, StatType.Spirit) * 2) + (GetPlayerLevel(index) * 2) + armor)

                ' * 1.5 if crit hit
                If CanNpcCrit(npcnum) Then
                    Damage = Damage * 1.5
                    SendActionMsg(mapNum, "Critical!", ColorType.BrightCyan, 1, (MapNPC(mapNum).Npc(mapnpcnum).X * 32), (MapNPC(mapNum).Npc(mapnpcnum).Y * 32))
                End If

            End If

            If Damage > 0 Then
                NpcAttackPlayer(mapnpcnum, index, Damage)
            End If

        End If

    End Sub

    Function CanNpcAttackPlayer(MapNpcNum As Integer, index As Integer) As Boolean
        Dim mapNum As Integer
        Dim NpcNum As Integer

        ' Check for subscript out of range
        If MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or Not IsPlaying(index) Then
            Exit Function
        End If

        ' Check for subscript out of range
        If MapNPC(GetPlayerMap(index)).Npc(MapNpcNum).Num <= 0 Then
            Exit Function
        End If

        mapNum = GetPlayerMap(index)
        NpcNum = MapNPC(mapNum).Npc(MapNpcNum).Num

        ' Make sure the npc isn't already dead
        If MapNPC(mapNum).Npc(MapNpcNum).Vital(VitalType.HP) <= 0 Then
            Exit Function
        End If

        ' Make sure npcs dont attack more then once a second
        If GetTimeMs() < MapNPC(mapNum).Npc(MapNpcNum).AttackTimer + 1000 Then
            Exit Function
        End If

        ' Make sure we dont attack the player if they are switching maps
        If TempPlayer(index).GettingMap = True Then
            Exit Function
        End If

        MapNPC(mapNum).Npc(MapNpcNum).AttackTimer = GetTimeMs()

        ' Make sure they are on the same map
        If IsPlaying(index) Then
            If NpcNum > 0 Then

                ' Check if at same coordinates
                If (GetPlayerY(index) + 1 = MapNPC(mapNum).Npc(MapNpcNum).Y) And (GetPlayerX(index) = MapNPC(mapNum).Npc(MapNpcNum).X) Then
                    CanNpcAttackPlayer = True
                Else

                    If (GetPlayerY(index) - 1 = MapNPC(mapNum).Npc(MapNpcNum).Y) And (GetPlayerX(index) = MapNPC(mapNum).Npc(MapNpcNum).X) Then
                        CanNpcAttackPlayer = True
                    Else

                        If (GetPlayerY(index) = MapNPC(mapNum).Npc(MapNpcNum).Y) And (GetPlayerX(index) + 1 = MapNPC(mapNum).Npc(MapNpcNum).X) Then
                            CanNpcAttackPlayer = True
                        Else

                            If (GetPlayerY(index) = MapNPC(mapNum).Npc(MapNpcNum).Y) And (GetPlayerX(index) - 1 = MapNPC(mapNum).Npc(MapNpcNum).X) Then
                                CanNpcAttackPlayer = True
                            End If
                        End If
                    End If
                End If
            End If
        End If

    End Function

    Function CanNpcAttackNpc(mapNum As Integer, Attacker As Integer, Victim As Integer) As Boolean
        Dim aNpcNum As Integer, vNpcNum As Integer, VictimX As Integer
        Dim VictimY As Integer, AttackerX As Integer, AttackerY As Integer

        CanNpcAttackNpc = False

        ' Check for subscript out of range
        If Attacker <= 0 Or Attacker > MAX_MAP_NPCS Then
            Exit Function
        End If

        If Victim <= 0 Or Victim > MAX_MAP_NPCS Then
            Exit Function
        End If

        ' Check for subscript out of range
        If MapNPC(mapNum).Npc(Attacker).Num <= 0 Then
            Exit Function
        End If

        ' Check for subscript out of range
        If MapNPC(mapNum).Npc(Victim).Num <= 0 Then
            Exit Function
        End If

        aNpcNum = MapNPC(mapNum).Npc(Attacker).Num
        vNpcNum = MapNPC(mapNum).Npc(Victim).Num

        If aNpcNum <= 0 Then Exit Function
        If vNpcNum <= 0 Then Exit Function

        ' Make sure the npcs arent already dead
        If MapNPC(mapNum).Npc(Attacker).Vital(VitalType.HP) < 0 Then
            Exit Function
        End If

        ' Make sure the npc isn't already dead
        If MapNPC(mapNum).Npc(Victim).Vital(VitalType.HP) < 0 Then
            Exit Function
        End If

        ' Make sure npcs dont attack more then once a second
        If GetTimeMs() < MapNPC(mapNum).Npc(Attacker).AttackTimer + 1000 Then
            Exit Function
        End If

        MapNPC(mapNum).Npc(Attacker).AttackTimer = GetTimeMs()

        AttackerX = MapNPC(mapNum).Npc(Attacker).X
        AttackerY = MapNPC(mapNum).Npc(Attacker).Y
        VictimX = MapNPC(mapNum).Npc(Victim).X
        VictimY = MapNPC(mapNum).Npc(Victim).Y

        ' Check if at same coordinates
        If (VictimY + 1 = AttackerY) And (VictimX = AttackerX) Then
            CanNpcAttackNpc = True
        Else

            If (VictimY - 1 = AttackerY) And (VictimX = AttackerX) Then
                CanNpcAttackNpc = True
            Else

                If (VictimY = AttackerY) And (VictimX + 1 = AttackerX) Then
                    CanNpcAttackNpc = True
                Else

                    If (VictimY = AttackerY) And (VictimX - 1 = AttackerX) Then
                        CanNpcAttackNpc = True
                    End If
                End If
            End If
        End If

    End Function

    Friend Sub NpcAttackPlayer(MapNpcNum As Integer, Victim As Integer, Damage As Integer)
        Dim Name As String, mapNum As Integer
        Dim z As Integer, InvCount As Integer, EqCount As Integer, j As Integer, x As Integer
        Dim buffer As New ByteStream(4)

        ' Check for subscript out of range
        If MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Or IsPlaying(Victim) = False Then Exit Sub

        ' Check for subscript out of range
        If MapNPC(GetPlayerMap(Victim)).Npc(MapNpcNum).Num <= 0 Then Exit Sub

        mapNum = GetPlayerMap(Victim)
        Name = Trim$(NPC(MapNPC(mapNum).Npc(MapNpcNum).Num).Name)

        ' Send this packet so they can see the npc attacking
        buffer.WriteInt32(ServerPackets.SNpcAttack)
        buffer.WriteInt32(MapNpcNum)
        SendDataToMap(mapNum, buffer.Data, buffer.Head)
        buffer.Dispose()

        If Damage <= 0 Then Exit Sub

        ' set the regen timer
        MapNPC(mapNum).Npc(MapNpcNum).StopRegen = True
        MapNPC(mapNum).Npc(MapNpcNum).StopRegenTimer = GetTimeMs()

        If Damage >= GetPlayerVital(Victim, VitalType.HP) Then
            ' Say damage
            SendActionMsg(GetPlayerMap(Victim), "-" & GetPlayerVital(Victim, VitalType.HP), ColorType.BrightRed, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))

            ' Set NPC target to 0
            MapNPC(mapNum).Npc(MapNpcNum).Target = 0
            MapNPC(mapNum).Npc(MapNpcNum).TargetType = 0

            ' kill player
            KillPlayer(Victim)

            ' Player is dead
            GlobalMsg(GetPlayerName(Victim) & " has been killed by " & Name)
        Else
            ' Player not dead, just do the damage
            SetPlayerVital(Victim, VitalType.HP, GetPlayerVital(Victim, VitalType.HP) - Damage)
            SendVital(Victim, VitalType.HP)
            SendAnimation(mapNum, NPC(MapNPC(GetPlayerMap(Victim)).Npc(MapNpcNum).Num).Animation, 0, 0, TargetType.Player, Victim)

            ' send the sound
            'SendMapSound Victim, GetPlayerX(Victim), GetPlayerY(Victim), SoundEntity.seNpc, MapNpc(MapNum).Npc(MapNpcNum).Num

            ' Say damage
            SendActionMsg(GetPlayerMap(Victim), "-" & Damage, ColorType.BrightRed, 1, (GetPlayerX(Victim) * 32), (GetPlayerY(Victim) * 32))
            SendBlood(GetPlayerMap(Victim), GetPlayerX(Victim), GetPlayerY(Victim))

            ' set the regen timer
            TempPlayer(Victim).StopRegen = True
            TempPlayer(Victim).StopRegenTimer = GetTimeMs()

        End If

    End Sub

    Sub NpcAttackNpc(mapNum As Integer, Attacker As Integer, Victim As Integer, Damage As Integer)
        Dim buffer As New ByteStream(4)
        Dim aNpcNum As Integer
        Dim vNpcNum As Integer
        Dim n As Integer

        If Attacker <= 0 Or Attacker > MAX_MAP_NPCS Then Exit Sub
        If Victim <= 0 Or Victim > MAX_MAP_NPCS Then Exit Sub

        If Damage <= 0 Then Exit Sub

        aNpcNum = MapNPC(mapNum).Npc(Attacker).Num
        vNpcNum = MapNPC(mapNum).Npc(Victim).Num

        If aNpcNum <= 0 Then Exit Sub
        If vNpcNum <= 0 Then Exit Sub

        ' Send this packet so they can see the person attacking
        buffer.WriteInt32(ServerPackets.SNpcAttack)
        buffer.WriteInt32(Attacker)
        SendDataToMap(mapNum, buffer.Data, buffer.Head)
        buffer.Dispose()

        If Damage >= MapNPC(mapNum).Npc(Victim).Vital(VitalType.HP) Then
            SendActionMsg(mapNum, "-" & Damage, ColorType.BrightRed, 1, (MapNPC(mapNum).Npc(Victim).X * 32), (MapNPC(mapNum).Npc(Victim).Y * 32))
            SendBlood(mapNum, MapNPC(mapNum).Npc(Victim).X, MapNPC(mapNum).Npc(Victim).Y)

            ' npc is dead.

            ' Set NPC target to 0
            MapNPC(mapNum).Npc(Attacker).Target = 0
            MapNPC(mapNum).Npc(Attacker).TargetType = 0

            ' Drop the goods if they get it
            Dim tmpitem = Random(1, 5)
            n = Int(Rnd() * NPC(vNpcNum).DropChance(tmpitem)) + 1
            If n = 1 Then
                SpawnItem(NPC(vNpcNum).DropItem(tmpitem), NPC(vNpcNum).DropItemValue(tmpitem), mapNum, MapNPC(mapNum).Npc(Victim).X, MapNPC(mapNum).Npc(Victim).Y)
            End If

            ' Reset victim's stuff so it dies in loop
            MapNPC(mapNum).Npc(Victim).Num = 0
            MapNPC(mapNum).Npc(Victim).SpawnWait = GetTimeMs()
            MapNPC(mapNum).Npc(Victim).Vital(VitalType.HP) = 0

            ' send npc death packet to map
            buffer = New ByteStream(4)
            buffer.WriteInt32(ServerPackets.SNpcDead)
            buffer.WriteInt32(Victim)
            SendDataToMap(mapNum, buffer.Data, buffer.Head)
            buffer.Dispose()
        Else
            ' npc not dead, just do the damage
            MapNPC(mapNum).Npc(Victim).Vital(VitalType.HP) = MapNPC(mapNum).Npc(Victim).Vital(VitalType.HP) - Damage
            ' Say damage
            SendActionMsg(mapNum, "-" & Damage, ColorType.BrightRed, 1, (MapNPC(mapNum).Npc(Victim).X * 32), (MapNPC(mapNum).Npc(Victim).Y * 32))
            SendBlood(mapNum, MapNPC(mapNum).Npc(Victim).X, MapNPC(mapNum).Npc(Victim).Y)
        End If

    End Sub

    Friend Sub KnockBackNpc(index As Integer, NpcNum As Integer, Optional IsSkill As Integer = 0)
        If IsSkill > 0 Then
            For i = 0 To Skill(IsSkill).KnockBackTiles
                If CanNpcMove(GetPlayerMap(index), NpcNum, GetPlayerDir(index)) Then
                    NpcMove(GetPlayerMap(index), NpcNum, GetPlayerDir(index), MovementType.Walking)
                End If
            Next
            MapNPC(GetPlayerMap(index)).Npc(NpcNum).StunDuration = 1
            MapNPC(GetPlayerMap(index)).Npc(NpcNum).StunTimer = GetTimeMs()
        Else
            If Item(GetPlayerEquipment(index, EquipmentType.Weapon)).KnockBack = 1 Then
                For i = 0 To Item(GetPlayerEquipment(index, EquipmentType.Weapon)).KnockBackTiles
                    If CanNpcMove(GetPlayerMap(index), NpcNum, GetPlayerDir(index)) Then
                        NpcMove(GetPlayerMap(index), NpcNum, GetPlayerDir(index), MovementType.Walking)
                    End If
                Next
                MapNPC(GetPlayerMap(index)).Npc(NpcNum).StunDuration = 1
                MapNPC(GetPlayerMap(index)).Npc(NpcNum).StunTimer = GetTimeMs()
            End If
        End If
    End Sub

    Friend Function RandomNpcAttack(mapNum As Integer, MapNpcNum As Integer) As Integer
        Dim i As Integer, SkillList As New List(Of Byte)

        RandomNpcAttack = 0

        If MapNPC(mapNum).Npc(MapNpcNum).SkillBuffer > 0 Then Exit Function

        For i = 1 To MAX_NPC_SKILLS
            If NPC(MapNPC(mapNum).Npc(MapNpcNum).Num).Skill(i) > 0 Then
                SkillList.Add(NPC(MapNPC(mapNum).Npc(MapNpcNum).Num).Skill(i))
            End If
        Next

        If SkillList.Count > 1 Then
            RandomNpcAttack = SkillList(Random(0, SkillList.Count - 1))
        Else
            RandomNpcAttack = 0
        End If

    End Function

    Friend Function GetNpcSkill(NpcNum As Integer, skillslot As Integer) As Integer
        GetNpcSkill = NPC(NpcNum).Skill(skillslot)
    End Function

    Friend Sub BufferNpcSkill(mapNum As Integer, MapNpcNum As Integer, skillslot As Integer)
        Dim skillnum As Integer
        Dim MPCost As Integer
        Dim SkillCastType As Integer
        Dim range As Integer
        Dim HasBuffered As Boolean

        Dim TargetType As Byte
        Dim Target As Integer

        ' Prevent subscript out of range
        If skillslot < 0 Or skillslot > MAX_NPC_SKILLS Then Exit Sub

        skillnum = GetNpcSkill(MapNPC(mapNum).Npc(MapNpcNum).Num, skillslot)

        If skillnum <= 0 Or skillnum > MAX_SKILLS Then Exit Sub

        ' see if cooldown has finished
        If MapNPC(mapNum).Npc(MapNpcNum).SkillCd(skillslot) > GetTimeMs() Then
            TryNpcAttackPlayer(MapNpcNum, MapNPC(mapNum).Npc(MapNpcNum).Target)
            Exit Sub
        End If

        MPCost = Skill(skillnum).MpCost

        ' Check if they have enough MP
        If MapNPC(mapNum).Npc(MapNpcNum).Vital(VitalType.SP) < MPCost Then Exit Sub

        ' find out what kind of skill it is! self cast, target or AOE
        If Skill(skillnum).Range > 0 Then
            ' ranged attack, single target or aoe?
            If Not Skill(skillnum).IsAoE Then
                SkillCastType = 2 ' targetted
            Else
                SkillCastType = 3 ' targetted aoe
            End If
        Else
            If Not Skill(skillnum).IsAoE Then
                SkillCastType = 0 ' self-cast
            Else
                SkillCastType = 1 ' self-cast AoE
            End If
        End If

        TargetType = MapNPC(mapNum).Npc(MapNpcNum).TargetType
        Target = MapNPC(mapNum).Npc(MapNpcNum).Target
        range = Skill(skillnum).Range
        HasBuffered = False

        Select Case SkillCastType
            Case 0, 1 ' self-cast & self-cast AOE
                HasBuffered = True
            Case 2, 3 ' targeted & targeted AOE
                ' check if have target
                If Not Target > 0 Then
                    Exit Sub
                End If
                If TargetType = Core.TargetType.Player Then
                    ' if have target, check in range
                    If Not IsInRange(range, MapNPC(mapNum).Npc(MapNpcNum).X, MapNPC(mapNum).Npc(MapNpcNum).Y, GetPlayerX(Target), GetPlayerY(Target)) Then
                        Exit Sub
                    Else
                        HasBuffered = True
                    End If
                ElseIf TargetType = Core.TargetType.Npc Then
                    '' if have target, check in range
                    'If Not isInRange(range, GetPlayerX(Index), GetPlayerY(Index), MapNpc(MapNum).Npc(Target).x, MapNpc(MapNum).Npc(Target).y) Then
                    '    PlayerMsg(Index, "Target not in range.")
                    '    HasBuffered = False
                    'Else
                    '    ' go through skill types
                    '    If Skill(skillnum).Type <> SkillType.DAMAGEHP And Skill(skillnum).Type <> SkillType.DAMAGEMP Then
                    '        HasBuffered = True
                    '    Else
                    '        If CanAttackNpc(Index, Target, True) Then
                    '            HasBuffered = True
                    '        End If
                    '    End If
                    'End If
                End If
        End Select

        If HasBuffered Then
            SendAnimation(mapNum, Skill(skillnum).CastAnim, 0, 0, Core.TargetType.Player, Target)
            MapNPC(mapNum).Npc(MapNpcNum).SkillBuffer = skillslot
            MapNPC(mapNum).Npc(MapNpcNum).SkillBufferTimer = GetTimeMs()
            Exit Sub
        End If
    End Sub

    Friend Function CanNpcBlock(npcnum As Integer) As Boolean
        Dim rate As Integer
        Dim stat As Integer
        Dim rndNum As Integer

        CanNpcBlock = False

        stat = NPC(npcnum).Stat(StatType.Luck) / 5  'guessed shield agility
        rate = stat / 12.08
        rndNum = Random(1, 100)

        If rndNum <= rate Then CanNpcBlock = True

    End Function

    Friend Function CanNpcCrit(npcnum As Integer) As Boolean
        Dim rate As Integer
        Dim rndNum As Integer

        CanNpcCrit = False

        rate = NPC(npcnum).Stat(StatType.Luck) / 3
        rndNum = Random(1, 100)

        If rndNum <= rate Then CanNpcCrit = True

    End Function

    Friend Function CanNpcDodge(npcnum As Integer) As Boolean
        Dim rate As Integer
        Dim rndNum As Integer

        CanNpcDodge = False

        rate = NPC(npcnum).Stat(StatType.Luck) / 4
        rndNum = Random(1, 100)

        If rndNum <= rate Then CanNpcDodge = True

    End Function

    Friend Function CanNpcParry(npcnum As Integer) As Boolean
        Dim rate As Integer
        Dim rndNum As Integer

        CanNpcParry = False

        rate = NPC(npcnum).Stat(StatType.Luck) / 6
        rndNum = Random(1, 100)

        If rndNum <= rate Then CanNpcParry = True

    End Function

    Function GetNpcDamage(npcnum As Integer) As Integer

        GetNpcDamage = (NPC(npcnum).Stat(StatType.Strength) * 2) + (NPC(npcnum).Damage * 2) + (NPC(npcnum).Level * 3) + Random(1, 20)

    End Function

    Friend Function IsNpcDead(mapNum As Integer, MapNpcNum As Integer)
        IsNpcDead = False
        If mapNum <= 0 Or mapNum > MAX_MAPS Or MapNpcNum <= 0 Or MapNpcNum > MAX_MAP_NPCS Then Exit Function
        If MapNPC(mapNum).Npc(MapNpcNum).Vital(VitalType.HP) <= 0 Then IsNpcDead = True
    End Function

    Friend Sub DropNpcItems(mapNum As Integer, MapNpcNum As Integer)
        Dim NpcNum = MapNPC(mapNum).Npc(MapNpcNum).Num
        Dim tmpitem = Random(1, 5)
        Dim n = Int(Rnd() * NPC(NpcNum).DropChance(tmpitem)) + 1

        If n = 1 Then
            SpawnItem(NPC(NpcNum).DropItem(tmpitem), NPC(NpcNum).DropItemValue(tmpitem), mapNum, MapNPC(mapNum).Npc(MapNpcNum).X, MapNPC(mapNum).Npc(MapNpcNum).Y)
        End If
    End Sub

#End Region

#Region "Outgoing Packets"

    Sub SendMapNpcsToMap(mapNum As Integer)
        Dim i As Integer
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ServerPackets.SMapNpcData)

        For i = 1 To MAX_MAP_NPCS
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Num)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).X)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Y)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Dir)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Vital(VitalType.HP))
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Vital(VitalType.SP))
        Next

        SendDataToMap(mapNum, buffer.Data, buffer.Head)

        buffer.Dispose()
    End Sub

#End Region

#Region "Incoming Packets"

    Sub Packet_EditNpc(index As Integer, ByRef data() As Byte)
        ' Prevent hacking
        If GetPlayerAccess(index) < AccessType.Developer Then Exit Sub
        If TempPlayer(index).Editor > 0 Then Exit Sub

        Dim user As String

        user = IsEditorLocked(index, EditorType.NPC)

        If user <> "" Then
            PlayerMsg(index, "The game editor is locked and being used by " + user + ".", ColorType.BrightRed)
            Exit Sub
        End If

        TempPlayer(index).Editor = EditorType.NPC

        SendItems(index)
        SendAnimations(index)
        SendSkills(index)
        SendNpcs(index)

        Dim Buffer = New ByteStream(4)
        Buffer.WriteInt32(ServerPackets.SNpcEditor)
        Socket.SendDataTo(index, Buffer.Data, Buffer.Head)

        Buffer.Dispose()
    End Sub

    Sub Packet_SaveNPC(index As Integer, ByRef data() As Byte)
        Dim NpcNum As Integer, i As Integer
        Dim buffer As New ByteStream(data)

        ' Prevent hacking
        If GetPlayerAccess(index) < AccessType.Developer Then Exit Sub

        NpcNum = buffer.ReadInt32

        ' Update the Npc
        NPC(NpcNum).Animation = buffer.ReadInt32()
        NPC(NpcNum).AttackSay = buffer.ReadString()
        NPC(NpcNum).Behaviour = buffer.ReadByte()

        For i = 1 To MAX_DROP_ITEMS
            NPC(NpcNum).DropChance(i) = buffer.ReadInt32()
            NPC(NpcNum).DropItem(i) = buffer.ReadInt32()
            NPC(NpcNum).DropItemValue(i) = buffer.ReadInt32()
        Next

        NPC(NpcNum).Exp = buffer.ReadInt32()
        NPC(NpcNum).Faction = buffer.ReadByte()
        NPC(NpcNum).HP = buffer.ReadInt32()
        NPC(NpcNum).Name = buffer.ReadString()
        NPC(NpcNum).Range = buffer.ReadByte()
        NPC(NpcNum).SpawnTime = buffer.ReadByte()
        NPC(NpcNum).SpawnSecs = buffer.ReadInt32()
        NPC(NpcNum).Sprite = buffer.ReadInt32()

        For i = 1 To StatType.Count - 1
            NPC(NpcNum).Stat(i) = buffer.ReadByte()
        Next

        For i = 1 To MAX_NPC_SKILLS
            NPC(NpcNum).Skill(i) = buffer.ReadByte()
        Next

        NPC(NpcNum).Level = buffer.ReadInt32()
        NPC(NpcNum).Damage = buffer.ReadInt32()

        ' Save it
        SendUpdateNpcToAll(NpcNum)
        SaveNpc(NpcNum)
        Addlog(GetPlayerLogin(index) & " saved Npc #" & NpcNum & ".", ADMIN_LOG)

        buffer.Dispose()
    End Sub

    Sub SendNpcs(index As Integer)
        Dim i As Integer

        For i = 1 To MAX_NPCS
            If Len(Trim$(NPC(i).Name)) > 0 Then
                SendUpdateNpcTo(index, i)
            End If
        Next

    End Sub

    Sub SendUpdateNpcTo(index As Integer, NpcNum As Integer)
        Dim buffer As ByteStream, i As Integer

        buffer = New ByteStream(4)
        buffer.WriteInt32(ServerPackets.SUpdateNpc)

        buffer.WriteInt32(NpcNum)
        buffer.WriteInt32(NPC(NpcNum).Animation)
        buffer.WriteString(NPC(NpcNum).AttackSay)
        buffer.WriteByte(NPC(NpcNum).Behaviour)

        For i = 1 To MAX_DROP_ITEMS
            buffer.WriteInt32(NPC(NpcNum).DropChance(i))
            buffer.WriteInt32(NPC(NpcNum).DropItem(i))
            buffer.WriteInt32(NPC(NpcNum).DropItemValue(i))
        Next

        buffer.WriteInt32(NPC(NpcNum).Exp)
        buffer.WriteByte(NPC(NpcNum).Faction)
        buffer.WriteInt32(NPC(NpcNum).HP)
        buffer.WriteString(NPC(NpcNum).Name)
        buffer.WriteByte(NPC(NpcNum).Range)
        buffer.WriteByte(NPC(NpcNum).SpawnTime)
        buffer.WriteInt32(NPC(NpcNum).SpawnSecs)
        buffer.WriteInt32(NPC(NpcNum).Sprite)

        For i = 1 To StatType.Count - 1
            buffer.WriteByte(NPC(NpcNum).Stat(i))
        Next

        For i = 1 To MAX_NPC_SKILLS
            buffer.WriteByte(NPC(NpcNum).Skill(i))
        Next

        buffer.WriteInt32(NPC(NpcNum).Level)
        buffer.WriteInt32(NPC(NpcNum).Damage)

        Socket.SendDataTo(index, buffer.Data, buffer.Head)
        buffer.Dispose()
    End Sub

    Sub SendUpdateNpcToAll(NpcNum As Integer)
        Dim buffer As ByteStream, i As Integer

        buffer = New ByteStream(4)
        buffer.WriteInt32(ServerPackets.SUpdateNpc)

        buffer.WriteInt32(NpcNum)
        buffer.WriteInt32(NPC(NpcNum).Animation)
        buffer.WriteString(NPC(NpcNum).AttackSay)
        buffer.WriteByte(NPC(NpcNum).Behaviour)

        For i = 1 To MAX_DROP_ITEMS
            buffer.WriteInt32(NPC(NpcNum).DropChance(i))
            buffer.WriteInt32(NPC(NpcNum).DropItem(i))
            buffer.WriteInt32(NPC(NpcNum).DropItemValue(i))
        Next

        buffer.WriteInt32(NPC(NpcNum).Exp)
        buffer.WriteByte(NPC(NpcNum).Faction)
        buffer.WriteInt32(NPC(NpcNum).HP)
        buffer.WriteString(NPC(NpcNum).Name)
        buffer.WriteByte(NPC(NpcNum).Range)
        buffer.WriteByte(NPC(NpcNum).SpawnTime)
        buffer.WriteInt32(NPC(NpcNum).SpawnSecs)
        buffer.WriteInt32(NPC(NpcNum).Sprite)

        For i = 1 To StatType.Count - 1
            buffer.WriteByte(NPC(NpcNum).Stat(i))
        Next

        For i = 1 To MAX_NPC_SKILLS
            buffer.WriteByte(NPC(NpcNum).Skill(i))
        Next

        buffer.WriteInt32(NPC(NpcNum).Level)
        buffer.WriteInt32(NPC(NpcNum).Damage)

        SendDataToAll(buffer.Data, buffer.Head)
        buffer.Dispose()
    End Sub

    Sub SendMapNpcsTo(index As Integer, mapNum As Integer)
        Dim i As Integer
        Dim buffer As ByteStream
        buffer = New ByteStream(4)

        buffer.WriteInt32(ServerPackets.SMapNpcData)

        For i = 1 To MAX_MAP_NPCS
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Num)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).X)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Y)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Dir)
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Vital(VitalType.HP))
            buffer.WriteInt32(MapNPC(mapNum).Npc(i).Vital(VitalType.SP))
        Next

        Socket.SendDataTo(index, buffer.Data, buffer.Head)

        buffer.Dispose()
    End Sub

    Sub SendMapNpcTo(mapNum As Integer, MapNpcNum As Integer)
        Dim buffer As ByteStream
        buffer = New ByteStream(4)

        buffer.WriteInt32(ServerPackets.SMapNpcUpdate)

        buffer.WriteInt32(MapNpcNum)

        With MapNPC(mapNum).Npc(MapNpcNum)
            buffer.WriteInt32(.Num)
            buffer.WriteInt32(.X)
            buffer.WriteInt32(.Y)
            buffer.WriteInt32(.Dir)
            buffer.WriteInt32(.Vital(VitalType.HP))
            buffer.WriteInt32(.Vital(VitalType.SP))
        End With

        SendDataToMap(mapNum, buffer.Data, buffer.Head)

        buffer.Dispose()
    End Sub

    Sub SendMapNpcVitals(mapNum As Integer, MapNpcNum As Byte)
        Dim i As Integer
        Dim buffer As ByteStream
        buffer = New ByteStream(4)

        buffer.WriteInt32(ServerPackets.SMapNpcVitals)
        buffer.WriteInt32(MapNpcNum)
        
        For i = 1 To VitalType.Count - 1
            buffer.WriteInt32(MapNPC(mapNum).Npc(MapNpcNum).Vital(i))
        Next

        SendDataToMap(mapNum, buffer.Data, buffer.Head)

        buffer.Dispose()
    End Sub

    Sub SendNpcAttack(index As Integer, NpcNum As Integer)
        Dim Buffer = New ByteStream(4)
        Buffer.WriteInt32(ServerPackets.SAttack)

        Buffer.WriteInt32(NpcNum)
        SendDataToMap(GetPlayerMap(index), Buffer.Data, Buffer.Head)
        Buffer.Dispose()
    End Sub

    Sub SendNpcDead(mapNum As Integer, index As Integer)
        Dim Buffer = New ByteStream(4)
        Buffer.WriteInt32(ServerPackets.SNpcDead)

        Buffer.WriteInt32(index)
        SendDataToMap(mapNum, Buffer.Data, Buffer.Head)
        Buffer.Dispose()
    End Sub

#End Region

End Module