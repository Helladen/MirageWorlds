﻿Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms.VisualStyles.VisualStyleElement.Window
Imports Core
Imports Mirage.Sharp.Asfw
Imports SFML.Audio

Module C_Animations

#Region "Globals"

    Friend AnimationIndex As Byte
    Friend AnimInstance() As Types.AnimInstanceStruct

#End Region

#Region "Database"

    Sub ClearAnimation(index As Integer)
        Animation(index) = Nothing
        Animation(index) = New AnimationStruct
        For X = 0 To 1
            ReDim Animation(index).Sprite(x)
        Next
        For X = 0 To 1
            ReDim Animation(index).Frames(x)
        Next
        For x = 0 To 1
            Animation(index).Frames(X) = 5
        Next
        For X = 0 To 1
            ReDim Animation(index).LoopCount(x)
        Next
        For X = 0 To 1
            ReDim Animation(index).LoopTime(x)
        Next
        Animation(index).Name = ""
        Animation(index).LoopCount(0) = 1
        Animation(index).LoopCount(1) = 1
        Animation(index).LoopTime(0) = 1
        Animation(index).LoopTime(1) = 1
        Animation_Loaded(index) = False
    End Sub

    Sub ClearAnimations()
        Dim i As Integer

        ReDim Animation(MAX_ANIMATIONS)

       For i = 1 To MAX_ANIMATIONS
            ClearAnimation(i)
        Next

    End Sub

    Sub ClearAnimInstances()
        Dim i As Integer

        ReDim AnimInstance(Byte.MaxValue)

        For i = 0 To Byte.MaxValue
            For X = 0 To 1
                ReDim AnimInstance(i).Timer(x)
            Next
            For X = 0 To 1
                ReDim AnimInstance(i).Used(x)
            Next
            For X = 0 To 1
                ReDim AnimInstance(i).LoopIndex(x)
            Next
            For X = 0 To 1
                ReDim AnimInstance(i).FrameIndex(x)
            Next

            ClearAnimInstance(i)
        Next
    End Sub

    Sub ClearAnimInstance(index As Integer)
        AnimInstance(index).Animation = 0
        AnimInstance(index).X = 0
        AnimInstance(index).Y = 0

        For i = 0 To UBound(AnimInstance(index).Used)
            AnimInstance(index).Used(i) = False
        Next

        For i = 0 To UBound(AnimInstance(index).Timer)
            
            AnimInstance(index).Timer(i) = False
        Next

        For i = 0 To UBound(AnimInstance(index).FrameIndex)
            AnimInstance(index).FrameIndex(i) = False
        Next

        AnimInstance(index).LockType = 0
        AnimInstance(index).lockindex = 0
    End Sub

    Sub StreamAnimation(animationNum As Integer)
        If animationNum > 0 and Animation(animationNum).Name = "" Or Animation_Loaded(animationNum) = False Then
            Animation_Loaded(animationNum) = True
            SendRequestAnimation(animationNum)
        End If
    End Sub

#End Region

#Region "Incoming Traffic"

    Sub Packet_UpdateAnimation(ByRef data() As Byte)
        Dim n As Integer, i As Integer
        Dim buffer As New ByteStream(data)

        n = buffer.ReadInt32
        ' Update the Animation
        For i = 0 To UBound(Animation(n).Frames)
            Animation(n).Frames(i) = buffer.ReadInt32()
        Next

        For i = 0 To UBound(Animation(n).LoopCount)
            Animation(n).LoopCount(i) = buffer.ReadInt32()
        Next

        For i = 0 To UBound(Animation(n).LoopTime)
            Animation(n).LoopTime(i) = buffer.ReadInt32()
        Next

        Animation(n).Name = Trim$(buffer.ReadString)
        Animation(n).Sound = Trim$(buffer.ReadString)

        If Animation(n).Name Is Nothing Then Animation(n).Name = ""
        If Animation(n).Sound Is Nothing Then Animation(n).Sound = ""

        For i = 0 To UBound(Animation(n).Sprite)
            Animation(n).Sprite(i) = buffer.ReadInt32()
        Next
        buffer.Dispose()
    End Sub

    Sub Packet_Animation(ByRef data() As Byte)
        Dim buffer As New ByteStream(data)

        AnimationIndex = AnimationIndex + 1
        If AnimationIndex >= Byte.MaxValue Then AnimationIndex = 1

        With AnimInstance(AnimationIndex)
            .Animation = buffer.ReadInt32
            .X = buffer.ReadInt32
            .Y = buffer.ReadInt32
            .LockType = buffer.ReadInt32
            .lockindex = buffer.ReadInt32
            .Used(0) = True
            .Used(1) = True
        End With

        buffer.Dispose()
    End Sub

#End Region

#Region "Outgoing Traffic"

    Sub SendRequestAnimation(animationNum as Integer)
        Dim buffer As New ByteStream(4)

        buffer.WriteInt32(ClientPackets.CRequestAnimation)
        
        buffer.WriteInt32(animationNum)

        Socket.SendData(buffer.Data, buffer.Head)
        buffer.Dispose()
    End Sub

#End Region

#Region "Drawing"

    Friend Sub DrawAnimation(index As Integer, layer As Integer)
        Dim sprite As Integer = Animation(AnimInstance(index).Animation).Sprite(layer)
        If sprite < 1 Or sprite > NumAnimations Then Return

        Dim graphicInfo As GraphicInfo = AnimationGfxInfo(sprite)

        ' Get dimensions and column count from controls and graphic info
        Dim totalWidth As Integer = graphicInfo.Width
        Dim totalHeight As Integer = graphicInfo.Height
        Dim columns As Integer = Animation(AnimInstance(index).Animation).Frames(layer)
        Dim frameWidth As Integer

        ' Calculate frame dimensions
        If columns > 0 Then
            framewidth = totalWidth / columns
        End If

        Dim frameHeight As Integer = frameWidth
        Dim rows As Integer
        If frameHeight > 0 Then
            rows = totalHeight / frameHeight
        End If

        Dim frameCount As Integer = rows * columns
        Dim frameIndex As Integer

        ' Calculate the current frame index
        If frameCount > 0 Then
            frameIndex = AnimInstance(index).FrameIndex(layer) Mod frameCount
        End If

        Dim column As Integer
        Dim row As Integer

        If columns > 0 Then
            column = frameIndex Mod columns
            row = frameIndex \ columns
        End If
        
        Dim sRect As New Rectangle(column * frameWidth, row * frameHeight, frameWidth, frameHeight)

        ' Determine the position based on lock type and instance status
        Dim x As Integer, y As Integer

        If AnimInstance(index).LockType > 0 Then
            Dim lockindex As Integer = AnimInstance(index).lockindex
            Dim point As Point = GetLockedPosition(index, lockindex, frameWidth, frameHeight)
            x = point.X
            y = point.Y
        Else
            x = (AnimInstance(index).X * 32) + 16 - (frameWidth / 2)
            y = (AnimInstance(index).Y * 32) + 16 - (frameHeight / 2)
        End If

        x = ConvertMapX(x)
        y = ConvertMapY(y)

        ' Render the frame using the calculated source rectangle and position
        RenderTexture(sprite, GfxType.Animation, Window, x, y, sRect.X, sRect.Y, frameWidth, frameHeight, frameWidth, frameHeight)
    End Sub

    Private Function GetLockedPosition(index As Integer, lockindex As Integer, width As Integer, height As Integer) As Point
        Dim x As Integer = 0
        Dim y As Integer = 0

        Select Case AnimInstance(index).LockType
            Case TargetType.Player
                If IsPlaying(lockindex) AndAlso GetPlayerMap(lockindex) = GetPlayerMap(MyIndex) Then
                    x = (GetPlayerX(lockindex) * PicX) + 16 - (width / 2) + Player(lockindex).XOffset
                    y = (GetPlayerY(lockindex) * PicY) + 16 - (height / 2) + Player(lockindex).YOffset
                End If
            Case TargetType.Npc
                If MapNpc(lockindex).Num > 0 AndAlso MapNpc(lockindex).Vital(VitalType.HP) > 0 Then
                    x = (MapNpc(lockindex).X * PicX) + 16 - (width / 2) + MapNpc(lockindex).XOffset
                    y = (MapNpc(lockindex).Y * PicY) + 16 - (height / 2) + MapNpc(lockindex).YOffset
                End If
            Case TargetType.Pet
                If IsPlaying(lockindex) AndAlso PetAlive(lockindex) AndAlso GetPlayerMap(lockindex) = GetPlayerMap(MyIndex) Then
                    x = (Player(lockindex).Pet.X * PicX) + 16 - (width / 2) + Player(lockindex).Pet.XOffset
                    y = (Player(lockindex).Pet.Y * PicY) + 16 - (height / 2) + Player(lockindex).Pet.YOffset
                End If
        End Select

        Return New Point(x,y)
    End Function

    Friend Sub CheckAnimInstance(index As Integer)
        Dim looptime As Integer
        Dim layer As Integer
        Dim sound As String

        ' if doesn't exist then exit sub
        If AnimInstance(index).Animation <= 0 Then Exit Sub
        If AnimInstance(index).Animation > MAX_ANIMATIONS Then Exit Sub

        StreamAnimation(AnimInstance(index).Animation)

        ' Get dimensions and column count from controls and graphic info
        Dim totalWidth As Integer = AnimationGfxInfo(AnimInstance(index).Animation).Width
        Dim totalHeight As Integer = AnimationGfxInfo(AnimInstance(index).Animation).Height
        Dim columns As Integer = Animation(AnimInstance(index).Animation).Frames(layer)

        ' Calculate frame dimensions
        Dim frameWidth As Integer = totalWidth / columns
        Dim frameHeight As Integer = frameWidth
        Dim rows As Integer
        If frameHeight > 0 Then
            rows = totalHeight / frameHeight
        End If

        Dim frameCount As Integer = rows * columns
        Dim frameIndex As Integer

        ' Calculate the current frame index
        If frameCount > 0 Then
            frameIndex = AnimInstance(index).FrameIndex(layer) Mod frameCount
        End If

        Dim column As Integer = frameIndex Mod columns
        Dim row As Integer = frameIndex \ columns

        For layer = 0 To 1
            If AnimInstance(index).Used(layer) Then
                looptime = Animation(AnimInstance(index).Animation).LoopTime(layer)

                ' if zero'd then set so we don't have extra loop and/or frame
                If AnimInstance(index).FrameIndex(layer) = 0 Then AnimInstance(index).FrameIndex(layer) = 1
                If AnimInstance(index).LoopIndex(layer) = 0 Then AnimInstance(index).LoopIndex(layer) = 1

                ' check if frame timer is set, and needs to have a frame change
                If AnimInstance(index).Timer(layer) + looptime <= GetTickCount() Then
                    ' check if out of range
                    If AnimInstance(index).FrameIndex(layer) >= frameCount Then
                        AnimInstance(index).LoopIndex(layer) = AnimInstance(index).LoopIndex(layer) + 1
                        If AnimInstance(index).LoopIndex(layer) > Animation(AnimInstance(index).Animation).LoopCount(layer) Then
                            AnimInstance(index).Used(layer) = False
                        Else
                            AnimInstance(index).FrameIndex(layer) = 1
                            sound = Animation(AnimInstance(index).Animation).Sound
                            If sound <> "" Then PlaySound(sound, AnimInstance(index).X, AnimInstance(index).Y)
                        End If
                    Else
                        AnimInstance(index).FrameIndex(layer) += 1
                    End If
                    AnimInstance(index).Timer(layer) = GetTickCount()
                End If
            End If
        Next

        ' if neither layer is used, clear
        If AnimInstance(index).Used(0) = False And AnimInstance(index).Used(1) = False Then
            ClearAnimInstance(index)
        End If
    End Sub

    Friend Sub CreateAnimation(animationNum As Integer, x As Byte, y As Byte)
        Dim sound As String

        AnimationIndex = AnimationIndex + 1
        If AnimationIndex >= Byte.MaxValue Then AnimationIndex = 1

        With AnimInstance(AnimationIndex)
            .Animation = animationNum
            .X = x
            .Y = y
            .LockType = 0
            .lockindex = 0
            .Used(0) = True
            .Used(1) = True

             sound = Animation(.Animation).Sound
            If sound <> "" Then PlaySound(sound, .X, .Y)
        End With
    End Sub

#End Region

End Module