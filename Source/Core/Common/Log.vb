﻿Imports System.IO

Public Module Log
     Public Function GetFileContents(fullPath As String) As String
        Dim strContents = ""
        Dim objReader As StreamReader

        If Not File.Exists(fullPath) Then File.Create(fullPath).Dispose()
        Try
            objReader = New StreamReader(fullPath)
            strContents = objReader.ReadToEnd()
            objReader.Close()
        Catch
        End Try
        Return strContents
    End Function

    Public Function Addlog(strData As String, FN As String) As Boolean
        Dim fullpath As String
        Dim contents As String
        Dim bAns = False
        Dim objReader As StreamWriter

        ' Check if the directory exists
        If Not Directory.Exists(Paths.Logs) Then
            ' Create the directory
            Directory.CreateDirectory(Paths.Logs)
        End If

        fullpath = Paths.Logs & FN
        contents = GetFileContents(fullpath)
        contents = contents & vbNewLine & strData

        Try
            objReader = New StreamWriter(fullpath)
            objReader.Write(contents)
            objReader.Close()
            bAns = True
        Catch
        End Try
        Return bAns
    End Function

    Public Function AddTextToFile(strData As String, fn As String) As Boolean
        Dim fullpath As String
        Dim contents As String
        Dim bAns = False
        Dim objReader As StreamWriter

        fullpath = Paths.Database & fn
        contents = GetFileContents(fullpath)
        contents = contents & vbNewLine & strData

        Try
            objReader = New StreamWriter(fullpath)
            objReader.Write(contents)
            objReader.Close()
            bAns = True
        Catch
        End Try
        Return bAns
    End Function
End Module
