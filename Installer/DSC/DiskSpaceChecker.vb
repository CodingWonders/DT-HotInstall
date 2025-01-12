Imports System.IO
Imports Microsoft.Dism

Public Class DiskSpaceChecker

    Dim progressMessage As String = ""

    Function GetSystemDrives() As DriveInfo()
        Return DriveInfo.GetDrives()
    End Function

    Function GetSystemBootDrive(FixedDrives As DriveInfo()) As DriveInfo
        If FixedDrives Is Nothing Then Return Nothing
        For Each FixedDrive As DriveInfo In FixedDrives
            If FixedDrive.Name = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) Then
                Return FixedDrive
            End If
        Next
        Return Nothing
    End Function

    Function GetDirectorySize(DirectoryName As String, ExcludedFile As String) As Long
        Dim DirectorySize As Long = 0
        If Directory.Exists(DirectoryName) Then
            Try
                For Each FileInDir In Directory.GetFiles(DirectoryName, "*", SearchOption.AllDirectories)
                    If Path.GetFileName(FileInDir).Equals(ExcludedFile, StringComparison.OrdinalIgnoreCase) Then Continue For
                    DirectorySize += New FileInfo(FileInDir).Length
                Next
            Catch ex As Exception

            End Try
        End If
        Return 0
    End Function

    Private Sub BackgroundWorker1_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles BackgroundWorker1.DoWork
        Try
            progressMessage = "Getting system drives..."
            Dim SystemDrives() As DriveInfo = GetSystemDrives()
            If SystemDrives.Length > 0 Then
                ' Only show drives that we have access to (are ready)
                SystemDrives = SystemDrives.Where(Function(drive) drive.IsReady)
                If SystemDrives.Any() Then
                    Dim FixedDrives() As DriveInfo = SystemDrives.Where(Function(drive) drive.DriveType = DriveType.Fixed)
                    Dim RemovableDrives() As DriveInfo = SystemDrives.Where(Function(drive) drive.DriveType = DriveType.Removable)

                    If Not FixedDrives.Any() And RemovableDrives.Any() Then
                        Throw New Exception("WARNING ONLY: Available drives were found, but they are all removable. This is not well supported. Proceed at your own risk.")
                    End If

                    ' Continue with the fixed drives
                    If FixedDrives.Any() Then
                        Dim SystemBootDrive As DriveInfo = GetSystemBootDrive(FixedDrives)
                        If SystemBootDrive IsNot Nothing Then
                            ' We have grabbed the system boot drive
                            Dim FolderSize As Long = GetDirectorySize(Path.GetPathRoot(Application.StartupPath), "install.wim")
                        End If

                    End If
                Else
                    Throw New Exception("WARNING ONLY: We could not detect available drives in your system. Proceed at your own risk.")
                End If
            Else
                Throw New Exception("WARNING ONLY: We could not detect the drives in your system. Proceed at your own risk.")
            End If
        Catch ex As Exception
            Throw ex
        End Try
    End Sub

    Private Sub BackgroundWorker1_ProgressChanged(sender As Object, e As System.ComponentModel.ProgressChangedEventArgs) Handles BackgroundWorker1.ProgressChanged
        Label2.Text = progressMessage
        ProgressBar1.Value = e.ProgressPercentage
    End Sub

    Private Sub BackgroundWorker1_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles BackgroundWorker1.RunWorkerCompleted
        If e.Error IsNot Nothing Then
            If e.Error.Message.StartsWith("WARNING ONLY: ", StringComparison.OrdinalIgnoreCase) Then
                MsgBox(e.Error.Message, vbOKOnly + vbExclamation, Text)
            Else
                Throw e.Error
            End If
        End If
        Close()
    End Sub

    Private Sub DiskSpaceChecker_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        BackgroundWorker1.RunWorkerAsync()
    End Sub
End Class