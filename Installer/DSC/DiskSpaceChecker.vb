﻿Imports System.IO
Imports Microsoft.Dism
Imports Microsoft.VisualBasic.ControlChars

Public Class DiskSpaceChecker

    Dim progressMessage As String = ""

    Dim reportContents As String = ""

    Dim isTestApplicable As Boolean
    Dim sourcePath As String = ""

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
            For Each FileInDir In Directory.GetFiles(DirectoryName, "*", SearchOption.AllDirectories)
                If Path.GetFileName(FileInDir).Equals(ExcludedFile, StringComparison.OrdinalIgnoreCase) Then Continue For
                DirectorySize += New FileInfo(FileInDir).Length
            Next
        End If
        Return DirectorySize
    End Function

    Sub InitializeReport()
        reportContents = "Disk Space Checker Report" & CrLf &
            "==========================" & CrLf &
            "Report generated by HotInstall (version " & My.Application.Info.Version.ToString() & ")" & CrLf & CrLf
    End Sub

    Sub ListObtainedDisks(FixedDrives As DriveInfo(), RemovableDrives As DriveInfo())
        reportContents &= "List of drives present in the host system:" & CrLf &
                          "- Fixed drives: " & FixedDrives.Length & CrLf &
                          "- Removable drives: " & RemovableDrives.Length & CrLf & CrLf

        If FixedDrives.Length > 0 Then
            reportContents &= "Information about the fixed drives:" & CrLf & CrLf
            For Each FixedDrive As DriveInfo In FixedDrives
                reportContents &= "- Drive letter: " & FixedDrive.Name & CrLf &
                                  "- Drive type: " & FixedDrive.DriveType.ToString() & CrLf &
                                  "- Drive format: " & FixedDrive.DriveFormat & CrLf &
                                  "- Drive label: " & FixedDrive.VolumeLabel & CrLf &
                                  "- Drive size: " & FixedDrive.TotalSize & " bytes (~" & Converters.BytesToReadableSize(FixedDrive.TotalSize) & ")" & CrLf &
                                  "- Free space: " & FixedDrive.TotalFreeSpace & " bytes (~" & Converters.BytesToReadableSize(FixedDrive.TotalFreeSpace) & ")" & CrLf &
                                  "- Is the system drive? " & If(FixedDrive.Name = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "Yes", "No") & CrLf & CrLf
            Next
        End If

        If RemovableDrives.Length > 0 Then
            reportContents &= "Information about the removable drives:" & CrLf & CrLf
            For Each RemovableDrive As DriveInfo In RemovableDrives
                reportContents &= "- Drive letter: " & RemovableDrive.Name & CrLf &
                                  "- Drive type: " & RemovableDrive.DriveType.ToString() & CrLf &
                                  "- Drive format: " & RemovableDrive.DriveFormat & CrLf &
                                  "- Drive label: " & RemovableDrive.VolumeLabel & CrLf &
                                  "- Drive size: " & RemovableDrive.TotalSize & " bytes (~" & Converters.BytesToReadableSize(RemovableDrive.TotalSize) & ")" & CrLf &
                                  "- Free space: " & RemovableDrive.TotalFreeSpace & " bytes (~" & Converters.BytesToReadableSize(RemovableDrive.TotalFreeSpace) & ")" & CrLf & CrLf &
                                  "- Is the system drive? " & If(RemovableDrive.Name = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "Yes", "No") & CrLf & CrLf
            Next
        End If

        If Not FixedDrives.Any() And RemovableDrives.Any() Then
            reportContents &= "All drives were detected to be removable" & CrLf & CrLf
        End If
    End Sub

    Sub ListFreeSpace(FreeSpace As Long, SpaceToCompare As Long)
        reportContents &= "Disc image files will be copied to the system drive shown above:" & CrLf &
                          "- The total size of the disc image files is " & SpaceToCompare & " bytes (~" & Converters.BytesToReadableSize(SpaceToCompare) & ")" & CrLf &
                          "- The free space on this drive is " & FreeSpace & " bytes (~" & Converters.BytesToReadableSize(FreeSpace) & ")" & CrLf & CrLf

        reportContents &= Converters.BytesToReadableSize(FreeSpace) & " > " & Converters.BytesToReadableSize(SpaceToCompare) & " ? " & If(FreeSpace > SpaceToCompare, "Yes", "No") & CrLf &
                          Converters.BytesToReadableSize(FreeSpace) & " > " & Converters.BytesToReadableSize(SpaceToCompare * 2) & " ? " & If(FreeSpace > SpaceToCompare, "Yes", "No") & CrLf & CrLf

        If FreeSpace < (SpaceToCompare * 2) Then
            reportContents &= "There may not be enough space to copy the disc image files to this drive." & CrLf & CrLf
        ElseIf FreeSpace < SpaceToCompare Then
            reportContents &= "There is not enough space to copy the disc image files to this drive." & CrLf & CrLf
        Else
            reportContents &= "There is plenty of space to copy the disc image files to this drive." & CrLf & CrLf
        End If
    End Sub

    Sub ListSpaceComparison(ImageNames As List(Of String), ImageSizes As List(Of Long), FixedDrives As DriveInfo())
        If ImageNames.Count = 0 Then
            Throw New Exception("No names have been passed")
        End If
        If ImageSizes.Count = 0 Then
            Throw New Exception("No sizes have been passed")
        End If
        If FixedDrives.Length = 0 Then
            Throw New Exception("No fixed drives have been passed")
        End If

        reportContents &= "Comparison of sizes:" & CrLf & CrLf

        For Each FixedDrive As DriveInfo In FixedDrives
            reportContents &= "- Drive, with label " & Quote & FixedDrive.VolumeLabel & Quote & " (" & FixedDrive.Name & "):" & CrLf
            For x = 0 To ImageSizes.Count - 1
                If FixedDrive.TotalSize > ImageSizes(x) Then
                    reportContents &= "  - " & Quote & ImageNames(x) & Quote & " (index " & x + 1 & ") can be installed on this drive because there is enough free space." & CrLf
                Else
                    reportContents &= "  - " & Quote & ImageNames(x) & Quote & " (index " & x + 1 & ") cannot be installed on this drive because there is not enough free space." & CrLf
                End If
            Next
        Next
        reportContents &= CrLf
    End Sub

    Sub SaveReport(Location As String)
        Try
            File.WriteAllText(Location, reportContents, System.Text.Encoding.UTF8)
        Catch ex As Exception

        End Try
    End Sub

    Private Sub BackgroundWorker1_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles BackgroundWorker1.DoWork
        InitializeReport()
        progressMessage = "Getting system drives..."
        BackgroundWorker1.ReportProgress(5)
        Dim SystemDrives() As DriveInfo = GetSystemDrives()
        If SystemDrives.Length > 0 Then
            ' Only show drives that we have access to (are ready)
            SystemDrives = SystemDrives.Where(Function(drive) drive.IsReady).ToArray()
            If SystemDrives.Any() Then
                Dim FixedDrives() As DriveInfo = SystemDrives.Where(Function(drive) drive.DriveType = DriveType.Fixed).ToArray()
                Dim RemovableDrives() As DriveInfo = SystemDrives.Where(Function(drive) drive.DriveType = DriveType.Removable).ToArray()

                ' Report information about the drives
                ListObtainedDisks(FixedDrives, RemovableDrives)

                If Not FixedDrives.Any() And RemovableDrives.Any() Then
                    Throw New Exception("Available drives were found, but they are all removable. This is not well supported..")
                End If

                ' Continue with the fixed drives
                If FixedDrives.Any() Then
                    Dim SystemBootDrive As DriveInfo = GetSystemBootDrive(FixedDrives)
                    If SystemBootDrive IsNot Nothing Then
                        ' We have grabbed the system boot drive
                        progressMessage = "Getting size of disc image files..."
                        BackgroundWorker1.ReportProgress(10)
                        Dim FolderSize As Long = GetDirectorySize(sourcePath, "")
                        ' Get the free space of the system boot drive, since we'll copy the files there
                        Dim FreeSpaceOnSystemDrive As Long = SystemBootDrive.TotalFreeSpace
                        ListFreeSpace(FreeSpaceOnSystemDrive, FolderSize)
                        ' Check if we have enough space
                        If FreeSpaceOnSystemDrive < FolderSize Then
                            Throw New Exception("There is not enough space to copy the disc image files to the system drive. Please free up some space and try again.")
                        End If
                        ' Get information about the installation image and compare the expanded sizes of all indexes with the total space of all fixed drives
                        progressMessage = "Getting image file information..."
                        BackgroundWorker1.ReportProgress(40)
                        Dim imgInfoCollection As DismImageInfoCollection = Nothing
                        Try
                            DismApi.Initialize(DismLogLevel.LogErrors)
                            imgInfoCollection = DismApi.GetImageInfo(Path.Combine(sourcePath, "sources", "install.wim"))
                        Catch ex As Exception
                            Throw ex
                        Finally
                            Try
                                DismApi.Shutdown()
                            Catch ex As Exception

                            End Try
                        End Try
                        If imgInfoCollection IsNot Nothing Then
                            progressMessage = "Getting image names and sizes..."
                            BackgroundWorker1.ReportProgress(60)
                            ' Grab the image names and expanded sizes
                            Dim ImageNames As New List(Of String)
                            Dim ImageSizes As New List(Of Long)
                            For Each imgInfo As DismImageInfo In imgInfoCollection
                                ImageNames.Add(imgInfo.ImageName)
                                ImageSizes.Add(imgInfo.ImageSize)
                            Next
                            progressMessage = "Comparing image sizes with free space..."
                            BackgroundWorker1.ReportProgress(80)
                            ListSpaceComparison(ImageNames, ImageSizes, FixedDrives)
                        End If
                    End If
                End If
            Else
                Throw New Exception("We could not detect available drives in your system.")
            End If
        Else
            Throw New Exception("We could not detect available drives in your system.")
        End If
    End Sub

    Private Sub BackgroundWorker1_ProgressChanged(sender As Object, e As System.ComponentModel.ProgressChangedEventArgs) Handles BackgroundWorker1.ProgressChanged
        Label2.Text = progressMessage
        ProgressBar1.Value = e.ProgressPercentage
        Application.DoEvents()
    End Sub

    Private Sub BackgroundWorker1_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles BackgroundWorker1.RunWorkerCompleted
        Dim success As Boolean = True
        If e.Error IsNot Nothing Then
            If e.Error.Message.StartsWith("WARNING ONLY: ", StringComparison.OrdinalIgnoreCase) Then
                MsgBox(e.Error.Message, vbOKOnly + vbExclamation, Text)
            Else
                success = False
                reportContents &= CrLf & CrLf & e.Error.ToString()
                Visible = False
            End If
        End If
        DialogResult = If(success, Windows.Forms.DialogResult.OK, Windows.Forms.DialogResult.Cancel)
        SaveReport(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
                                "DscReport.txt"))
        Close()
    End Sub

    Private Sub DiskSpaceChecker_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        isTestApplicable = MainForm.TestMode Or MainForm.TestBCD
        If isTestApplicable Then
            sourcePath = Application.StartupPath
        Else
            sourcePath = Path.GetPathRoot(Application.StartupPath)
        End If
        Dim IsDarkMode As Boolean = Utilities.IsSystemInDarkMode
        If Utilities.IsWindowsVersionOrGreater(10, 0, 18362) Then Utilities.EnableDarkTitleBar(Handle, IsDarkMode)
        BackColor = If(IsDarkMode, Color.FromArgb(12, 12, 12), Color.FromArgb(246, 246, 249))
        ForeColor = If(IsDarkMode, Color.White, Color.Black)
        BackgroundWorker1.RunWorkerAsync()
    End Sub
End Class