﻿Imports System.Net
Imports System.Net.Http

Namespace Network
    Public Module Api
        Const apiKey As String = "-5xPMTEPy3151vdLJKzaE04dGTnOu7vu"
        Const apiUrl As String = "https://giamon.com/forums/index.php/api/auth"

        Public Function Auth(username As String, password as String)
            Using client As System.Net.Http.HttpClient = New System.Net.Http.HttpClient()

                Dim params = New Dictionary(Of String, String) From {
                        {"login", username},
                        {"password", password}
                        }

                client.DefaultRequestHeaders.Add("XF-Api-Key", apiKey)

                Dim result As HttpResponseMessage = client.PostAsync(apiUrl, 
                                                                     New FormUrlEncodedContent(params)).Result
                If (result.StatusCode = HttpStatusCode.OK) Then ' HttpStatusCode.OK corresponds to HTTP status 200
                    Return True
                Else
                    Return False
                End If

            End Using

        End Function
        
    End Module
End Namespace