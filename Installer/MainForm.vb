Imports Microsoft.Dism
Imports System.IO
Imports Microsoft.VisualBasic.ControlChars
Imports System.Management
Imports System.ComponentModel

Public Class MainForm

    Dim CurrentWizardPage As New WizardPage()
    Dim VerifyInPages As New List(Of WizardPage.Page)

    Dim TestMode As Boolean = Environment.GetCommandLineArgs().Contains("/test")
    Dim TestBCD As Boolean = Environment.GetCommandLineArgs().Contains("/bcdtest")

    Dim BootArchitecture As Integer
    Dim ComputerArchitecture As Integer

    Dim BootMgrEntryName As String = "PE Helper BOOTMGR Bootstrapper (Codename HotInstall)"
    Dim SlideshowPicture As Integer = 0

    Dim ProgressMessage As String = ""

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        VerifyInPages.AddRange(New WizardPage.Page() {WizardPage.Page.DisclaimerPage, WizardPage.Page.ImageInfoPage})

        Dim bootPath As String = Path.Combine(Path.GetPathRoot(Application.StartupPath), "sources", "boot.wim")
        Dim installPath As String = Path.Combine(Path.GetPathRoot(Application.StartupPath), "sources", "install.wim")

        If TestMode Or TestBCD Then
            bootPath = Path.Combine(Application.StartupPath, "sources", "boot.wim")
            installPath = Path.Combine(Application.StartupPath, "sources", "install.wim")
        End If

        Dim BootImageInfo As DismImageInfoCollection = GetImageInformation(bootPath)
        Dim InstallImageInfo As DismImageInfoCollection = GetImageInformation(installPath)

        If BootImageInfo IsNot Nothing AndAlso InstallImageInfo IsNot Nothing Then
            Label11.Text = BootImageInfo(0).ImageName
            Label12.Text = BootImageInfo(0).ProductVersion.ToString()
            BootArchitecture = CInt(BootImageInfo(0).Architecture)
            Label13.Text = CastCPUArchitecture(BootArchitecture)

            For Each InstallationImage As DismImageInfo In InstallImageInfo
                ListView1.Items.Add(New ListViewItem(New String() {InstallationImage.ImageIndex, InstallationImage.ImageName, InstallationImage.ImageDescription, InstallationImage.ProductVersion.ToString(), CastCPUArchitecture(CInt(InstallationImage.Architecture))}))
            Next
        End If

        ' Get CPU architecture
        Dim Proc_Searcher As ManagementObjectSearcher = New ManagementObjectSearcher("SELECT Architecture FROM Win32_Processor")
        Dim Proc_Results As ManagementObjectCollection = Proc_Searcher.Get()
        If Proc_Results IsNot Nothing Then
            ComputerArchitecture = Proc_Results(0)("Architecture")
            Label6.Text = CastCPUArchitecture(ComputerArchitecture)
        End If

        Label7.Visible = Not GetArchitectureCompatibility(BootArchitecture, ComputerArchitecture)

        Label20.Text = Label20.Text.Replace("<entry>", BootMgrEntryName)

        If TestMode Then
            Text &= " (TEST MODE)"
        End If
        If TestBCD Then
            Text &= " (BCD TEST MODE)"
        End If

    End Sub

    Public Function CastCPUArchitecture(Architecture As Integer) As String
        Select Case Architecture
            Case 0
                Return "x86"
            Case 1
                Return "MIPS"
            Case 2
                Return "DEC Alpha"
            Case 3
                Return "PowerPC"
            Case 5
                Return "ARM"
            Case 6
                Return "Itanium"
            Case 9
                Return "AMD64"
            Case 12
                Return "ARM64"
            Case Else
                Return ""
        End Select
    End Function

    Public Function GetArchitectureCompatibility(ReferenceArchitecture As Integer, ComputerArchitecture As Integer) As Boolean
        Select Case ComputerArchitecture
            Case 0
                If ReferenceArchitecture > ComputerArchitecture Then
                    ' Everything is incompatible
                    Return False
                End If
            Case 5
                If ReferenceArchitecture <> ComputerArchitecture Then
                    ' Everything is incompatible
                    Return False
                End If
            Case 9
                If ReferenceArchitecture <> 9 AndAlso ReferenceArchitecture <> 0 Then
                    ' Everything apart from AMD64 or x86 is incompatible
                    Return False
                End If
            Case 12
                If Not ReferenceArchitecture <> 12 AndAlso Not ReferenceArchitecture <> 5 Then
                    ' Everything apart from ARM64 or ARM is incompatible
                    Return False
                End If
        End Select
        Return True
    End Function

    Private Sub ExitButton_Click(sender As Object, e As EventArgs) Handles ExitButton.Click
        Close()
    End Sub

    Private Sub NextButton_Click(sender As Object, e As EventArgs) Handles NextButton.Click
        If CurrentWizardPage.InstallerWizardPage = WizardPage.Page.FinishPage Then
            Close()
        Else
            ChangePage(CurrentWizardPage.InstallerWizardPage + 1)
        End If
    End Sub

    Private Sub BackButton_Click(sender As Object, e As EventArgs) Handles BackButton.Click
        ChangePage(CurrentWizardPage.InstallerWizardPage - 1)
    End Sub

    Function VerifyOptionsInPage(WizardPage As WizardPage.Page) As Boolean
        Select Case WizardPage
            Case Installer.WizardPage.Page.DisclaimerPage
                If Not CheckBox1.Checked Then
                    MsgBox("You must agree to the important notices before proceeding", vbOKOnly + vbCritical, Text)
                    Return False
                End If
            Case Installer.WizardPage.Page.ImageInfoPage
                If MsgBox("Does this disc image have the image you want to test?", vbYesNo + vbQuestion, Text) = MsgBoxResult.No Then
                    Return False
                End If
        End Select
        Return True
    End Function

    Sub ChangePage(NewPage As WizardPage.Page)
        If NewPage > CurrentWizardPage.InstallerWizardPage AndAlso VerifyInPages.Contains(CurrentWizardPage.InstallerWizardPage) Then
            If Not VerifyOptionsInPage(CurrentWizardPage.InstallerWizardPage) Then Exit Sub
        End If

        DisclaimerPanel.Visible = (NewPage = WizardPage.Page.DisclaimerPage)
        ImageInfoPanel.Visible = (NewPage = WizardPage.Page.ImageInfoPage)
        ExplanationPanel.Visible = (NewPage = WizardPage.Page.ExplanationPage)
        InstallationPanel.Visible = (NewPage = WizardPage.Page.InstallationPage)
        FinishPanel.Visible = (NewPage = WizardPage.Page.FinishPage)

        CurrentWizardPage.InstallerWizardPage = NewPage

        BackButton.Enabled = Not (NewPage = WizardPage.Page.DisclaimerPage) And Not (NewPage = WizardPage.Page.FinishPage)
        NextButton.Enabled = Not (NewPage = WizardPage.Page.FinishPage) And Not (NewPage + 1 >= WizardPage.PageCount)
        ExitButton.Enabled = Not (NewPage = WizardPage.Page.FinishPage)

        If NewPage = WizardPage.Page.InstallationPage Then
            SlideshowTimer.Enabled = True
            BackButton.Enabled = False
            NextButton.Enabled = False
            ExitButton.Enabled = False
            ControlBox = False
            InstallerBW.RunWorkerAsync()
        End If

        If NewPage = WizardPage.Page.FinishPage Then
            ButtonContainerPanel.Visible = False
        End If
    End Sub

    Public Function GetImageInformation(WindowsImage As String) As DismImageInfoCollection
        Try
            DismApi.Initialize(DismLogLevel.LogErrors)
            If File.Exists(WindowsImage) Then
                Return DismApi.GetImageInfo(WindowsImage)
            Else
                Throw New Exception("The Windows image " & Quote & WindowsImage & Quote & " does not exist in the file system")
            End If
        Catch ex As Exception
            MsgBox(ex.Message, vbOKOnly + vbCritical, Text)
            Return Nothing
        Finally
            Try
                DismApi.Shutdown()
            Catch ex As Exception
                ' Do nothing
            End Try
        End Try
        Return Nothing
    End Function

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If InstallerBW.IsBusy Then
            e.Cancel = True
            Beep()
            Exit Sub
        End If
        If CurrentWizardPage.InstallerWizardPage <> WizardPage.Page.FinishPage AndAlso MsgBox("Are you sure that you want to exit the installer?", vbYesNo + vbQuestion, Text) = MsgBoxResult.No Then
            e.Cancel = True
            Beep()
            Exit Sub
        End If
        Try
            If TestMode Then Directory.Delete(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT"), True)
            Directory.Delete(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS"), True)
        Catch ex As Exception

        End Try
    End Sub

    Private Sub SlideshowTimer_Tick(sender As Object, e As EventArgs) Handles SlideshowTimer.Tick
        BootMgrStep.Visible = (SlideshowPicture = 0)
        DTPEStep.Visible = (SlideshowPicture = 1)
        WindowsStep.Visible = (SlideshowPicture = 2)

        SlideshowPicture += 1
        If SlideshowPicture > 2 Then
            SlideshowPicture = 0
        End If
    End Sub

#Region "System Preparation Work"

    Sub CopyFiles(Source As String, Destination As String, Optional excludedFile As String = "")
        Try
            If Not Directory.Exists(Destination) Then
                Directory.CreateDirectory(Destination)
            End If
            Dim FileCount As Integer = Directory.GetFiles(Source, "*", SearchOption.AllDirectories).Count
            Dim CopiedFiles As Integer = 0

            Dim SourceRoot As String = Path.GetFullPath(Source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            Dim DestinationRoot As String = Path.GetFullPath(Destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

            For Each DirToCreate In Directory.GetDirectories(Source, "*", SearchOption.AllDirectories)
                Dim sourcePath As String = DirToCreate.Substring(SourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                Dim destinationPath As String = Path.Combine(DestinationRoot, sourcePath)
                If Not Directory.Exists(destinationPath) Then
                    Directory.CreateDirectory(destinationPath)
                End If
            Next

            For Each FileToCopy In Directory.GetFiles(Source, "*", SearchOption.AllDirectories)
                ProgressMessage = "Copying files from disc image... (Items copied thus far: " & CopiedFiles & "/" & FileCount & ")"
                InstallerBW.ReportProgress(5)
                If Path.GetFileName(FileToCopy) = excludedFile Then
                    CopiedFiles += 1
                    Continue For
                End If
                Dim sourcePath As String = FileToCopy.Substring(SourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                Dim destinationPath As String = Path.Combine(DestinationRoot, sourcePath)
                File.Copy(FileToCopy, destinationPath, True)
                CopiedFiles += 1
            Next
        Catch ex As Exception
            Throw ex
        End Try
    End Sub

    Sub UseWindowsImage(ImageFile As String, MountDirectory As String, Optional Index As Integer = 0, Optional Mount As Boolean = False, Optional Commit As Boolean = False)
        ' Check if things exist
        If Not File.Exists(ImageFile) Then Throw New Exception("The Windows image " & Quote & ImageFile & Quote & " does not exist in the file system.")
        Try
            If Not Directory.Exists(MountDirectory) Then
                Directory.CreateDirectory(MountDirectory)
            End If
        Catch ex As Exception
            Throw ex
        End Try

        ' Proceed with the DISM operation
        Try
            DismApi.Initialize(DismLogLevel.LogErrors)
            If Mount Then
                If Index <= 0 Then
                    Throw New Exception("When mounting an image, the index must be greater than 0")
                End If
                DismApi.MountImage(ImageFile, MountDirectory, Index, False)
            Else
                DismApi.UnmountImage(MountDirectory, Commit)
            End If
        Catch ex As Exception
            Throw ex
        Finally
            Try
                DismApi.Shutdown()
            Catch ex As Exception
                ' Don't do anything
            End Try
        End Try
    End Sub

    Public Function RunBCDConfigurator(Arguments As String) As Integer
        BCDEditProcess.StartInfo.Arguments = Arguments
        BCDEditProcess.Start()
        BCDEditProcess.WaitForExit()
        Return BCDEditProcess.ExitCode
    End Function

    Sub RunBCDConfiguration()
        Try
            BCDEditProcess.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "bcdedit.exe")
            Dim TargetGuidOutput As String = ""
            Dim TargetGuid As String = ""

            ' Configure bootmgr to use legacy view
            ProgressMessage = "Preparing to update boot configuration..."
            InstallerBW.ReportProgress(20)
            RunBCDConfigurator("/set {default} bootmenupolicy legacy")
            RunBCDConfigurator("/set {current} bootmenupolicy legacy")
            RunBCDConfigurator("/set {bootmgr} bootmenupolicy legacy")
            RunBCDConfigurator("/set {bootmgr} timeout 3")

            ' Configure RAMDisk Settings
            ProgressMessage = "Updating RAMDisk configuration..."
            InstallerBW.ReportProgress(25)
            RunBCDConfigurator("/create {ramdiskoptions}")
            RunBCDConfigurator("/set {ramdiskoptions} ramdisksdidevice partition=" & Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Replace("\", "").Trim())
            RunBCDConfigurator("/set {ramdiskoptions} ramdisksdipath \$DISMTOOLS.~BT\Boot\Boot.sdi")
            RunBCDConfigurator("/deletevalue {ramdiskoptions} description")     ' Necessary to delete if description is already in ramdisksettings

            ' Create BCD Entry and grab GUID
            ProgressMessage = "Creating boot entry..."
            InstallerBW.ReportProgress(30)
            BCDEditProcess.StartInfo.Arguments = "/create /d " & Quote & BootMgrEntryName & Quote & " /application osloader"
            BCDEditProcess.Start()
            TargetGuidOutput = BCDEditProcess.StandardOutput.ReadToEnd()
            BCDEditProcess.WaitForExit()
            If BCDEditProcess.ExitCode <> 0 Then Throw New Win32Exception(BCDEditProcess.ExitCode, "Boot entry creation has failed with the following error:")

            ' Extract GUID
            Dim startIndex As Integer = TargetGuidOutput.IndexOf("{")
            Dim endIndex As Integer = TargetGuidOutput.LastIndexOf("}")
            TargetGuid = TargetGuidOutput.Substring(startIndex, endIndex - startIndex + 1)
            File.WriteAllText(Application.StartupPath & "\bcdguid.txt", TargetGuid)

            ' Update BCD Entry
            ProgressMessage = "Configuring boot entry..."
            InstallerBW.ReportProgress(35)
            Dim osloaderPath As String = ""
            If Environment.GetEnvironmentVariable("FIRMWARE_TYPE") = "UEFI" Then
                osloaderPath = "\Windows\system32\Boot\winload.efi"
            Else
                osloaderPath = "\Windows\system32\winload.exe"
            End If
            RunBCDConfigurator("/set " & TargetGuid & " device ramdisk=[" & Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Replace("\", "").Trim() & "]\$DISMTOOLS.~BT\sources\boot.wim,{ramdiskoptions}")
            RunBCDConfigurator("/set " & TargetGuid & " osdevice ramdisk=[" & Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Replace("\", "").Trim() & "]\$DISMTOOLS.~BT\sources\boot.wim,{ramdiskoptions}")
            RunBCDConfigurator("/set " & TargetGuid & " path " & osloaderPath)
            RunBCDConfigurator("/set " & TargetGuid & " locale en-US")
            RunBCDConfigurator("/set " & TargetGuid & " systemroot \Windows")
            RunBCDConfigurator("/set " & TargetGuid & " detecthal Yes")
            RunBCDConfigurator("/set " & TargetGuid & " winpe Yes")
            ProgressMessage = "Modifying display order..."
            InstallerBW.ReportProgress(38)
            RunBCDConfigurator("/displayorder " & TargetGuid & " /addfirst")
            RunBCDConfigurator("/default " & TargetGuid)

        Catch ex As Exception
            Throw ex
        End Try
    End Sub

#End Region

    Private Sub InstallerBW_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles InstallerBW.DoWork
        ProgressMessage = "Creating temporary directory and copying files..."
        InstallerBW.ReportProgress(5)
        CopyFiles(If(TestMode Or TestBCD, Application.StartupPath, Path.GetPathRoot(Application.StartupPath)), Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT"), "install.wim")
        If TestMode Or TestBCD Then
            ' Leave bcdedit stuff out of test mode
            ProgressMessage = "Updating boot configuration..."
            InstallerBW.ReportProgress(20)
            RunBCDConfiguration()
        End If
        ProgressMessage = "Mounting Preinstallation Environment image..."
        InstallerBW.ReportProgress(40)
        UseWindowsImage(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT", "sources", "boot.wim"),
                        Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS"),
                        1, True)
        ProgressMessage = "Performing customizations..."
        InstallerBW.ReportProgress(60)
        Try
            File.WriteAllText(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS", "HotInstall"), "")
        Catch ex As Exception
            Throw ex
        End Try
        ProgressMessage = "Unmounting Preinstallation Environment image..."
        InstallerBW.ReportProgress(80)
        ' Unmount Windows image committing changes
        UseWindowsImage(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT", "sources", "boot.wim"),
                        Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS"),
                        0, False, True)
        ProgressMessage = "Deleting temporary files..."
        InstallerBW.ReportProgress(95)
        Try
            If TestMode Then Directory.Delete(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT"), True)
            Directory.Delete(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS"), True)
        Catch ex As Exception

        End Try
        InstallerBW.ReportProgress(100)
        ' Throw a signal to finish background worker and enter the finish page
        Throw New Exception("Preparation Finished")
    End Sub

    Private Sub InstallerBW_ProgressChanged(sender As Object, e As System.ComponentModel.ProgressChangedEventArgs) Handles InstallerBW.ProgressChanged
        Label19.Text = ProgressMessage
        ProgressBar1.Value = e.ProgressPercentage
    End Sub

    Private Sub InstallerBW_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles InstallerBW.RunWorkerCompleted
        If e.Error IsNot Nothing Then
            If e.Error.Message = "Preparation Finished" Then
                SlideshowTimer.Enabled = False
                ChangePage(CurrentWizardPage.InstallerWizardPage + 1)
                Exit Sub
            End If
            MsgBox(e.Error.ToString(), vbOKOnly + vbCritical, Text)
        End If
    End Sub

    Private Sub RestartButton_Click(sender As Object, e As EventArgs) Handles RestartButton.Click
        If Not TestMode Then
            Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "shutdown.exe"),
                          "/r /t 0")
        End If
        Close()
    End Sub
End Class
