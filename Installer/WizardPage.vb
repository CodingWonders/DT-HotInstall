Public Class WizardPage

    Public Enum Page As Integer
        DisclaimerPage = 0
        ImageInfoPage = 1
        ExplanationPage = 2
        InstallationPage = 3
        FinishPage = 4
    End Enum

    Public Property InstallerWizardPage As Page
    Public Const PageCount As Integer = 5

End Class
