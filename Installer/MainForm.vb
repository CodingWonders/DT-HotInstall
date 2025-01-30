Imports Microsoft.Dism
Imports System.IO
Imports Microsoft.VisualBasic.ControlChars
Imports System.Management
Imports System.ComponentModel
Imports System.Drawing

Public Class MainForm

    Dim CurrentWizardPage As New WizardPage()
    Dim VerifyInPages As New List(Of WizardPage.Page)

    Public TestMode As Boolean = Environment.GetCommandLineArgs().Contains("/test")
    Public TestBCD As Boolean = Environment.GetCommandLineArgs().Contains("/bcdtest")

    Dim BootArchitecture As Integer
    Dim ComputerArchitecture As Integer

    Dim BCDEntryTextLocation As String

    Dim BootMgrEntryName As String
    Dim SlideshowPicture As Integer = 1

    Dim ProgressMessage As String = ""

    Dim DismProgressPercentage As Integer = 0

    Dim CurrentStage As InstallationStage.InstallerStage

    ' Restart Timer
    Dim TimeTaken As Integer

    Sub ChangeLanguage(LanguageCode As String)
        If Not File.Exists(Path.Combine(Application.StartupPath, "Languages", "lang_" & LanguageCode & ".ini")) Then
            LanguageCode = "en"
        End If
        LoadLanguageFile(Path.Combine(Application.StartupPath, "Languages", "lang_" & LanguageCode & ".ini"))
        BackButton.Text = GetValueFromLanguageData("Common.Common_Back")
        NextButton.Text = GetValueFromLanguageData("Common.Common_Next")
        ExitButton.Text = GetValueFromLanguageData("Common.Common_Cancel")
        BootMgrEntryName = GetValueFromLanguageData("MainForm.BootMgrEntryName")
        Text = GetValueFromLanguageData("MainForm.WndTitle")
        Label1.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_Header")
        Label2.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_Description")
        TabPage1.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_ContentTabTitle1")
        TabPage2.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_ContentTabTitle2")
        TabPage3.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_ContentTabTitle3")
        TextBox1.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_Warranties")
        TextBox2.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_UseOfDiscImages")
        CheckBox1.Text = GetValueFromLanguageData("MainForm.DisclaimerPanel_DisclaimerCheck")
        Label4.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_Header")
        Label3.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_Description")
        GroupBox1.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_BootImageInfoGroup")
        Label8.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_BootImageName")
        Label9.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_BootImageVersion")
        Label10.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_BootImageArchitecture")
        GroupBox2.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_InstallImageInfoGroup")
        ListView1.Columns(1).Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_InstallImageName")
        ListView1.Columns(2).Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_InstallImageDescription")
        ListView1.Columns(3).Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_InstallImageVersion")
        ListView1.Columns(4).Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_InstallImageArchitecture")
        Label7.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_ImageArchitectureMismatchError")
        Label5.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_ComputerArchitecture")
        Label14.Text = GetValueFromLanguageData("MainForm.ReviewImageInfo_DIM_Notice")
        Label16.Text = GetValueFromLanguageData("MainForm.ExplanationPanel_Header")
        Label15.Text = GetValueFromLanguageData("MainForm.ExplanationPanel_Description")
        Label18.Text = GetValueFromLanguageData("MainForm.PreparationPanel_Header")
        Label17.Text = GetValueFromLanguageData("MainForm.PreparationPanel_Description")
        Label20.Text = GetValueFromLanguageData("MainForm.PreparationPanel_Step1").Replace("<entry>", BootMgrEntryName)
        Label27.Text = GetValueFromLanguageData("MainForm.PreparationPanel_Step2")
        Label31.Text = GetValueFromLanguageData("MainForm.PreparationPanel_Step3")
        Label33.Text = GetValueFromLanguageData("MainForm.FinishPanel_Header")
        Label32.Text = GetValueFromLanguageData("MainForm.FinishPanel_Description")
        Label35.Text = GetValueFromLanguageData("MainForm.FinishPanel_RestartTimer_Beginning")
        RestartButton.Text = GetValueFromLanguageData("MainForm.FinishPanel_RestartNow")
        Label36.Text = GetValueFromLanguageData("MainForm.ErrorPanel_Header")
        Label37.Text = GetValueFromLanguageData("MainForm.ErrorPanel_Description")
        Label38.Text = GetValueFromLanguageData("MainForm.ErrorPanel_PossibleFixes")
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Visible = False
        ChangeLanguage(My.Computer.Info.InstalledUICulture.TwoLetterISOLanguageName)

        ' Because of the DISM API, Windows 7 compatibility is out the window (no pun intended)
        If Environment.OSVersion.Version.Major = 6 And Environment.OSVersion.Version.Minor < 2 Then
            Throw New Exception(GetValueFromLanguageData("MainForm.Win7IncompatibilityError"))
        End If
        ' Check if the account has the required privileges
        If Not My.User.IsInRole(ApplicationServices.BuiltInRole.Administrator) Then
            Throw New Exception(GetValueFromLanguageData("MainForm.NonAdminError"))
        End If

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

        TextBox3.Text = My.Resources.Licenses

        Dim ScreenBounds As Rectangle = Screen.PrimaryScreen.Bounds
        StartPosition = FormStartPosition.Manual
        Location = New Point(
            (ScreenBounds.Width - Width) / 2,
            (ScreenBounds.Height - Height) / 2
            )

        Dim IsDarkMode As Boolean = Utilities.IsSystemInDarkMode

        If Utilities.IsWindowsVersionOrGreater(10, 0, 18362) Then Utilities.EnableDarkTitleBar(Handle, IsDarkMode)

        BackColor = If(IsDarkMode, Color.FromArgb(31, 31, 31), Color.White)
        ForeColor = If(IsDarkMode, Color.White, Color.Black)
        ButtonContainerPanel.BackColor = If(IsDarkMode, Color.FromArgb(48, 48, 48), Color.FromArgb(239, 239, 242))
        GroupBox1.BackColor = BackColor
        GroupBox1.ForeColor = ForeColor
        GroupBox2.BackColor = BackColor
        GroupBox2.ForeColor = ForeColor
        ListView1.BackColor = BackColor
        ListView1.ForeColor = ForeColor
        TabPage1.BackColor = BackColor
        TabPage1.ForeColor = ForeColor
        TabPage2.BackColor = BackColor
        TabPage2.ForeColor = ForeColor
        TabPage3.BackColor = BackColor
        TabPage3.ForeColor = ForeColor
        TextBox1.BackColor = BackColor
        TextBox1.ForeColor = ForeColor
        TextBox2.BackColor = BackColor
        TextBox2.ForeColor = ForeColor
        TextBox3.BackColor = BackColor
        TextBox3.ForeColor = ForeColor
        ErrorTextBox.BackColor = BackColor
        ErrorTextBox.ForeColor = ForeColor

        PictureBox3.Image = If(IsDarkMode, My.Resources.hotinstall_step3_dm, My.Resources.hotinstall_step3)

        Visible = True
    End Sub

    ''' <summary>
    ''' Gives a string representation of a CPU architecture
    ''' </summary>
    ''' <param name="Architecture">The number of the CPU architecture</param>
    ''' <returns>A string representation of <see>Architecture</see></returns>
    ''' <remarks></remarks>
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

    ''' <summary>
    ''' Gets a compatibility status between 2 architectures, a reference architecture and the computer architecture
    ''' </summary>
    ''' <param name="ReferenceArchitecture">The architecture to compare</param>
    ''' <param name="ComputerArchitecture">The architecture of the computer's processor, obtained via WMI</param>
    ''' <returns>A boolean determining the compatibility status</returns>
    ''' <remarks></remarks>
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

    ''' <summary>
    ''' Verifies options in a page before switching to a different one
    ''' </summary>
    ''' <param name="WizardPage">The current page the user is in</param>
    ''' <returns>A validation result</returns>
    ''' <remarks></remarks>
    Function VerifyOptionsInPage(WizardPage As WizardPage.Page) As Boolean
        Select Case WizardPage
            Case Installer.WizardPage.Page.DisclaimerPage
                If Not CheckBox1.Checked Then
                    MsgBox(GetValueFromLanguageData("MainForm.VERIFY_Disclaimer_Error"), vbOKOnly + vbCritical, Text)
                    Return False
                End If
            Case Installer.WizardPage.Page.ImageInfoPage
                If MsgBox(GetValueFromLanguageData("MainForm.VERIFY_ImageInfo_Question"), vbYesNo + vbQuestion, Text) = MsgBoxResult.No Then
                    Return False
                End If
        End Select
        Return True
    End Function

    ''' <summary>
    ''' Changes the current page the user is in to another one
    ''' </summary>
    ''' <param name="NewPage">The new page to change to</param>
    ''' <param name="Force">(Optional) Determines whether or not to skip checks</param>
    ''' <remarks></remarks>
    Sub ChangePage(NewPage As WizardPage.Page, Optional Force As Boolean = False)
        If NewPage > CurrentWizardPage.InstallerWizardPage AndAlso VerifyInPages.Contains(CurrentWizardPage.InstallerWizardPage) AndAlso Not Force Then
            If Not VerifyOptionsInPage(CurrentWizardPage.InstallerWizardPage) Then Exit Sub
        End If

        DisclaimerPanel.Visible = (NewPage = WizardPage.Page.DisclaimerPage)
        ImageInfoPanel.Visible = (NewPage = WizardPage.Page.ImageInfoPage)
        ExplanationPanel.Visible = (NewPage = WizardPage.Page.ExplanationPage)
        InstallationPanel.Visible = (NewPage = WizardPage.Page.InstallationPage)
        FinishPanel.Visible = (NewPage = WizardPage.Page.FinishPage)
        ErrorPanel.Visible = (NewPage = WizardPage.Page.FailurePage)

        If NewPage = WizardPage.Page.InstallationPage Or NewPage = WizardPage.Page.FinishPage Then
            ButtonContainerPanel.Visible = False
        End If

        CurrentWizardPage.InstallerWizardPage = NewPage

        BackButton.Enabled = Not (NewPage = WizardPage.Page.DisclaimerPage) And Not ((NewPage = WizardPage.Page.FinishPage) Or (NewPage = WizardPage.Page.FailurePage))
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

        If NewPage = WizardPage.Page.FailurePage Then
            ControlBox = True           ' At least let the user close the window
        End If
    End Sub

    ''' <summary>
    ''' Gets information about a specified image file
    ''' </summary>
    ''' <param name="WindowsImage">The absolute path of the Windows image file (in WIM format)</param>
    ''' <returns>An image information collection</returns>
    ''' <remarks></remarks>
    Public Function GetImageInformation(WindowsImage As String) As DismImageInfoCollection
        Try
            DismApi.Initialize(DismLogLevel.LogErrors)
            If File.Exists(WindowsImage) Then
                Return DismApi.GetImageInfo(WindowsImage)
            Else
                Throw New Exception(String.Format(GetValueFromLanguageData("MainForm.GetImageInfo_FileDoesNotExistError"), WindowsImage), New Win32Exception(2))
            End If
        Catch ex As Exception
            Throw
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
        If (CurrentWizardPage.InstallerWizardPage <> WizardPage.Page.FinishPage And CurrentWizardPage.InstallerWizardPage <> WizardPage.Page.FailurePage) AndAlso MsgBox(GetValueFromLanguageData("MainForm.ClosureQuestion"), vbYesNo + vbQuestion, Text) = MsgBoxResult.No Then
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

    ''' <summary>
    ''' Copies files from a given source to a given destination, whilst excluding any items whose names match the given exclusion
    ''' </summary>
    ''' <param name="Source">The source folder to copy files from</param>
    ''' <param name="Destination">The destination folder to copy files to</param>
    ''' <param name="ExcludedFile">The file to exclude from the copy process</param>
    ''' <remarks></remarks>
    Sub CopyFiles(Source As String, Destination As String, Optional ExcludedFile As String = "")
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
                ProgressMessage = String.Format(GetValueFromLanguageData("MainForm.CopyFiles_ProgressMessage"), CopiedFiles, FileCount)
                InstallerBW.ReportProgress(5)
                If Path.GetFileName(FileToCopy) = ExcludedFile Then
                    CopiedFiles += 1
                    Continue For
                End If
                Dim sourcePath As String = FileToCopy.Substring(SourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                Dim destinationPath As String = Path.Combine(DestinationRoot, sourcePath)
                File.Copy(FileToCopy, destinationPath, True)
                CopiedFiles += 1
                File.SetAttributes(destinationPath, FileAttributes.Archive)
            Next
        Catch ex As Exception
            Throw
        End Try
    End Sub

    ''' <summary>
    ''' Performs basic image file management with the provided Windows image
    ''' </summary>
    ''' <param name="ImageFile">The source image file to manage</param>
    ''' <param name="MountDirectory">A location where the image is mounted or will be mounted to</param>
    ''' <param name="Index">(Optional) The source index to mount</param>
    ''' <param name="Mount">(Optional) Determines whether or not to mount an image</param>
    ''' <param name="Commit">(Optional) Determines whether or not to save changes</param>
    ''' <remarks>If Mount is true, an index must be specified.</remarks>
    Sub UseWindowsImage(ImageFile As String, MountDirectory As String, Optional Index As Integer = 0, Optional Mount As Boolean = False, Optional Commit As Boolean = False)
        ' Check if things exist
        If Not File.Exists(ImageFile) Then Throw New Exception(String.Format(GetValueFromLanguageData("MainForm.GetImageInfo_FileDoesNotExistError"), ImageFile))
        Try
            If Not Directory.Exists(MountDirectory) Then
                Directory.CreateDirectory(MountDirectory)
            End If
        Catch ex As Exception
            Throw
        End Try

        DismProgressPercentage = 0
        Dim MountString As String
        Dim UnmountString As String

        MountString = ProgressMessage
        UnmountString = ProgressMessage

        ' Proceed with the DISM operation
        Try
            DismApi.Initialize(DismLogLevel.LogErrors)
            If Mount Then
                If Index <= 0 Then
                    Throw New Exception(GetValueFromLanguageData("MainForm.UseWindowsImage_Mount_IndexLT1"))
                End If
                DismApi.MountImage(ImageFile, MountDirectory, Index, False, Sub(progress As DismProgress)
                                                                                If progress.Current > 100 Then Exit Sub
                                                                                DismProgressPercentage = progress.Current
                                                                                ProgressMessage = MountString & " (" & DismProgressPercentage & "%)"
                                                                                InstallerBW.ReportProgress(ProgressBar1.Value)
                                                                            End Sub)
            Else
                DismApi.UnmountImage(MountDirectory, Commit, Sub(progress As DismProgress)
                                                                 If (progress.Current / 2) > 100 Then Exit Sub
                                                                 DismProgressPercentage = progress.Current / 2
                                                                 ProgressMessage = UnmountString & " (" & DismProgressPercentage & "%)"
                                                                 InstallerBW.ReportProgress(ProgressBar1.Value)
                                                             End Sub)
            End If
        Catch ex As Exception
            Throw
        Finally
            Try
                DismApi.Shutdown()
            Catch ex As Exception
                ' Don't do anything
            End Try
        End Try
    End Sub

    ''' <summary>
    ''' Runs BCDEdit with the provided arguments
    ''' </summary>
    ''' <param name="Arguments">The command-line arguments to pass to the command</param>
    ''' <param name="DontWorryBeHappy">(Optional) Determines whether or not to throw an exception if the process exits with a code different from 0</param>
    ''' <remarks>Arguments need to be passed. Otherwise, BCDEdit will simply return a basic list of entries on the BCD</remarks>
    Public Sub RunBCDConfigurator(Arguments As String, Optional DontWorryBeHappy As Boolean = False)
        Try
            BCDEditProcess.StartInfo.Arguments = Arguments
            BCDEditProcess.Start()
            BCDEditProcess.WaitForExit()
            If Not DontWorryBeHappy And BCDEditProcess.ExitCode <> 0 Then
                Throw New Exception(String.Format(GetValueFromLanguageData("MainForm.BCDEditConfiguratorError"), Arguments, Hex(BCDEditProcess.ExitCode), New Win32Exception(BCDEditProcess.ExitCode).Message))
            End If
        Catch ex As Exception
            Throw
        End Try
    End Sub

    ''' <summary>
    ''' Performs modifications to the Boot Configuration Data (BCD) of the system
    ''' </summary>
    ''' <remarks></remarks>
    Sub RunBCDConfiguration()
        Try
            BCDEditProcess.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "bcdedit.exe")
            Dim TargetGuidOutput As String = ""
            Dim TargetGuid As String = ""

            ' Configure bootmgr to use legacy view
            ProgressMessage = GetValueFromLanguageData("MainForm.BCDEditProcess_Preparation")
            InstallerBW.ReportProgress(20)
            RunBCDConfigurator("/set {default} bootmenupolicy legacy")
            RunBCDConfigurator("/set {current} bootmenupolicy legacy")
            RunBCDConfigurator("/set {bootmgr} timeout 3")

            ' Configure RAMDisk Settings
            ProgressMessage = GetValueFromLanguageData("MainForm.BCDEditProcess_RAMDiskConfig")
            InstallerBW.ReportProgress(25)
            RunBCDConfigurator("/create {ramdiskoptions}", True)
            RunBCDConfigurator("/set {ramdiskoptions} ramdisksdidevice partition=" & Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Replace("\", "").Trim())
            RunBCDConfigurator("/set {ramdiskoptions} ramdisksdipath \$DISMTOOLS.~BT\Boot\Boot.sdi")

            ' Create BCD Entry and grab GUID
            ProgressMessage = GetValueFromLanguageData("MainForm.BCDEditProcess_BootEntryCreate")
            InstallerBW.ReportProgress(30)
            BCDEditProcess.StartInfo.Arguments = "/create /d " & Quote & BootMgrEntryName & Quote & " /application osloader"
            BCDEditProcess.Start()
            TargetGuidOutput = BCDEditProcess.StandardOutput.ReadToEnd()
            BCDEditProcess.WaitForExit()
            If BCDEditProcess.ExitCode <> 0 Then Throw New Exception(String.Format(GetValueFromLanguageData("MainForm.BCDEditConfiguratorError_Simple"), Hex(BCDEditProcess.ExitCode), New Win32Exception(BCDEditProcess.ExitCode).Message))

            ' Extract GUID
            Dim startIndex As Integer = TargetGuidOutput.IndexOf("{")
            Dim endIndex As Integer = TargetGuidOutput.LastIndexOf("}")
            TargetGuid = TargetGuidOutput.Substring(startIndex, endIndex - startIndex + 1)
            If TestMode AndAlso TestBCD Then
                BCDEntryTextLocation = Path.Combine(Application.StartupPath, "bcdguid.txt")
            ElseIf Not TestMode Then
                BCDEntryTextLocation = Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "bcdguid.txt")
            End If
            File.WriteAllText(BCDEntryTextLocation, TargetGuid)

            ' Update BCD Entry
            ProgressMessage = GetValueFromLanguageData("MainForm.BCDEditProcess_BootEntryConfig")
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
            ProgressMessage = GetValueFromLanguageData("MainForm.BCDEditProcess_BootEntryDispOrderModify")
            InstallerBW.ReportProgress(38)
            RunBCDConfigurator("/displayorder " & TargetGuid & " /addfirst")
            RunBCDConfigurator("/default " & TargetGuid)

        Catch ex As Exception
            Throw
        End Try
    End Sub

#End Region

    Private Sub InstallerBW_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles InstallerBW.DoWork
        Control.CheckForIllegalCrossThreadCalls = False
        CurrentStage = InstallationStage.InstallerStage.DiskSpaceChecker
        If DiskSpaceChecker.ShowDialog(Me) = Windows.Forms.DialogResult.Cancel Then
            Throw New Exception(GetValueFromLanguageData("MainForm.DSC_ReportGen_Error"))
        End If
        Control.CheckForIllegalCrossThreadCalls = True
        CurrentStage = InstallationStage.InstallerStage.FileCopy
        ProgressMessage = GetValueFromLanguageData("MainForm.ProgressMessage_FileCopy")
        InstallerBW.ReportProgress(5)
        CopyFiles(If(TestMode Or TestBCD, Application.StartupPath, Path.GetPathRoot(Application.StartupPath)), Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT"), "install.wim")
        If Not TestMode OrElse (TestMode AndAlso TestBCD) Then
            CurrentStage = InstallationStage.InstallerStage.BootEntryCreation
            ' Leave bcdedit stuff out of test mode
            ProgressMessage = GetValueFromLanguageData("MainForm.ProgressMessage_BootEntryCreation")
            InstallerBW.ReportProgress(20)
            RunBCDConfiguration()
        End If
        CurrentStage = InstallationStage.InstallerStage.WIMMount
        ProgressMessage = GetValueFromLanguageData("MainForm.ProgressMessage_WIMMount")
        InstallerBW.ReportProgress(40)
        UseWindowsImage(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT", "sources", "boot.wim"),
                        Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS"),
                        1, True)
        ProgressMessage = GetValueFromLanguageData("MainForm.ProgressMessage_WIMCustomize")
        InstallerBW.ReportProgress(60)
        Try
            If TestMode And Not TestBCD Then Exit Try
            CurrentStage = InstallationStage.InstallerStage.WIMCustomize
            Dim HotInstallInfoPath As String = Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS", "HotInstall")
            If Not Directory.Exists(HotInstallInfoPath) Then
                Directory.CreateDirectory(HotInstallInfoPath)
            End If
            File.WriteAllText(Path.Combine(HotInstallInfoPath, "BcdEntry"), File.ReadAllText(BCDEntryTextLocation))
            If File.Exists(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "DscReport.txt")) Then
                File.Move(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "DscReport.txt"),
                          Path.Combine(HotInstallInfoPath, "DscReport.txt"))
            End If
        Catch ex As Exception
            Throw ex
        End Try
        CurrentStage = InstallationStage.InstallerStage.WIMUnmount
        ProgressMessage = GetValueFromLanguageData("MainForm.ProgressMessage_WIMUnmount")
        InstallerBW.ReportProgress(80)
        ' Unmount Windows image committing changes
        UseWindowsImage(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~BT", "sources", "boot.wim"),
                        Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), "$DISMTOOLS.~WS"),
                        0, False, True)
        ProgressMessage = GetValueFromLanguageData("MainForm.ProgressMessage_DeleteFiles")
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
        Label34.Text = "API Progress: " & DismProgressPercentage & "%"
    End Sub

    Private Sub InstallerBW_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles InstallerBW.RunWorkerCompleted
        If e.Error IsNot Nothing Then
            If e.Error.Message = "Preparation Finished" Then
                SlideshowTimer.Enabled = False
                Timer1.Enabled = True
                ChangePage(CurrentWizardPage.InstallerWizardPage + 1)
                Exit Sub
            Else
                LogErrorMessage(e.Error, CurrentStage)
                ChangePage(WizardPage.Page.FailurePage)
            End If
        End If
    End Sub

    Private Sub RestartButton_Click(sender As Object, e As EventArgs) Handles RestartButton.Click
        If Not TestMode Then
            Dim Shutter As New Process
            Shutter.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "system32", "shutdown.exe")
            Shutter.StartInfo.Arguments = "/r /t 0"
            Shutter.StartInfo.CreateNoWindow = True
            Shutter.StartInfo.WindowStyle = ProcessWindowStyle.Hidden
            Shutter.Start()
        End If
        Close()
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        TimeTaken += 1
        ProgressBar2.Value = TimeTaken * 10
        Dim messageIdentifier As String = ""
        If (10 - TimeTaken) = 1 Then
            messageIdentifier = "MainForm.AutoRestartMessage_Single"
        Else
            messageIdentifier = "MainForm.AutoRestartMessage_Multiple"
        End If
        Label35.Text = String.Format(GetValueFromLanguageData(messageIdentifier), 10 - TimeTaken)
        If TimeTaken >= 10 Then
            Timer1.Enabled = False
            RestartButton.PerformClick()
        End If
    End Sub

    ''' <summary>
    ''' Logs an error message caused by an exception
    ''' </summary>
    ''' <param name="ex">The exception that was caught</param>
    ''' <param name="stage">The stage at which the installer was</param>
    ''' <remarks></remarks>
    Sub LogErrorMessage(ex As Exception, stage As InstallationStage.InstallerStage)
        If ex Is Nothing Then Exit Sub

        Dim stageStr As String = ""
        ErrorTextBox.Clear()
        Select Case stage
            Case InstallationStage.InstallerStage.DiskSpaceChecker
                ' Close DSC and BGProcs
                DiskSpaceChecker.Dispose()
                InstallerBW.CancelAsync()
                stageStr = GetValueFromLanguageData("MainForm.ExceptionLogger_DSCStage")
            Case InstallationStage.InstallerStage.FileCopy
                stageStr = GetValueFromLanguageData("MainForm.ExceptionLogger_FileCopyStage")
            Case InstallationStage.InstallerStage.BootEntryCreation
                stageStr = GetValueFromLanguageData("MainForm.ExceptionLogger_BootEntryCreationStage")
            Case InstallationStage.InstallerStage.WIMMount
                stageStr = GetValueFromLanguageData("MainForm.ExceptionLogger_WIMMountStage")
            Case InstallationStage.InstallerStage.WIMCustomize
                stageStr = GetValueFromLanguageData("MainForm.ExceptionLogger_WIMCustomizeStage")
            Case InstallationStage.InstallerStage.WIMUnmount
                stageStr = GetValueFromLanguageData("MainForm.ExceptionLogger_WIMUnmountStage")
            Case InstallationStage.InstallerStage.Miscellaneous
                stageStr = GetValueFromLanguageData("MainForm.ExceptionLogger_Miscellaneous")
        End Select

        ErrorTextBox.AppendText(String.Format(GetValueFromLanguageData("MainForm.ExceptionLogger_ReportHeader"), stageStr))

        ErrorTextBox.AppendText(ex.Message & CrLf)
        ErrorTextBox.AppendText(String.Format(GetValueFromLanguageData("MainForm.ExceptionLogger_ErrorCodePara"), Hex(ex.HResult), New Win32Exception(ex.HResult).Message))

        ErrorTextBox.AppendText(GetValueFromLanguageData("MainForm.ExceptionLogger_IssueReportLink"))
    End Sub
End Class
