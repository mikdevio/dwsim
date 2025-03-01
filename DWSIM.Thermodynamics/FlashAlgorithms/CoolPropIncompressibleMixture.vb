﻿'    Copyright 2018 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports DWSIM.MathOps.MathEx


Namespace PropertyPackages.Auxiliary.FlashAlgorithms

    <System.Serializable()> Public Class CoolPropIncompressibleMixture

        Inherits FlashAlgorithm

        Dim spp As CoolPropIncompressibleMixturePropertyPackage

        Public Overrides ReadOnly Property InternalUseOnly As Boolean
            Get
                Return True
            End Get
        End Property

        Sub New()
            MyBase.New()
        End Sub

        Public Overrides ReadOnly Property AlgoType As Interfaces.Enums.FlashMethod
            Get
                Return Interfaces.Enums.FlashMethod.CoolProp_IncompressibleMixtures
            End Get
        End Property

        Public Overrides ReadOnly Property Description As String
            Get
                Return "CoolProp Incompressible Mixtures"
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "CoolProp Incompressible Mixtures"
            End Get
        End Property

        Public Overrides Function Flash_PT(ByVal Vz As Double(), ByVal P As Double, ByVal T As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim Psat, vf, lf As Double

            spp = PP

            Dim Vxw = spp.AUX_CONVERT_MOL_TO_MASS(Vz)
            Dim x = Vxw(Array.IndexOf(spp.RET_VNAMES(), spp.SoluteCompound))

            Psat = spp.AUX_PVAPi2(x, T)

            If P > Psat Then
                vf = 0.0#
            Else
                vf = 1.0#
            End If
            lf = 1 - vf

            Return New Object() {lf, vf, Vz.Clone, Vz.Clone, 0, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides Function Flash_PH(ByVal Vz As Double(), ByVal P As Double, ByVal H As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim vf, vfant, lf, T, Tmin, Tmax, Tmaxs, Tsat, Tant, Hl, Hv, Hlsat, Hvsat As Double

            spp = PP

            Dim si = Array.IndexOf(spp.RET_VNAMES(), spp.SoluteCompound)
            Dim nsi = Array.IndexOf(spp.RET_VNAMES(), spp.SolventCompound)
            Dim xmax = spp.SolutionDataList(spp.SoluteName).xmax

            Dim Vxw = spp.AUX_CONVERT_MOL_TO_MASS(Vz)
            Dim x = Vxw(si)

            Tmin = CoolProp.Props1SI(spp.GetCoolPropName(x), "TMIN")
            Tmax = CoolProp.Props1SI(spp.GetCoolPropName(x), "TMAX")
            Tmaxs = CoolProp.Props1SI(spp.SolventCompound, "TMAX")

            Dim Vx = Vz.Clone
            Dim Vy = spp.RET_NullVector
            Vy(nsi) = 1.0

            T = Tref

            Tsat = spp.AUX_TSAT(P, x)

            With spp

                Hlsat = spp.DW_CalcEnthalpy(Vz, Tsat, P, State.Liquid)
                Hvsat = spp.DW_CalcEnthalpy(Vz, Tsat, P, State.Vapor)

                vfant = vf

                Tant = T
                If H <= Hlsat Then
                    vf = 0.0#
                    lf = 1.0
                    Vx = Vz.Clone
                    Vy = PP.RET_NullVector
                    Vy(nsi) = 1.0
                    Dim bs As New BrentOpt.BrentMinimize
                    bs.DefineFuncDelegate(Function(tx)
                                              Hl = spp.DW_CalcEnthalpy(Vx, tx, P, State.Liquid)
                                              If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                  If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                      PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                                                  End If
                                              End If
                                              Return (H - Hl) ^ 2
                                          End Function)
                    Dim fmin As Double
                    fmin = bs.brentoptimize(Tmin, Tmax, 0.0001, T)
                ElseIf H >= Hvsat Then
                    vf = 1.0#
                    lf = 1 - vf
                    Vy = Vz.Clone
                    Vx(nsi) = 1.0
                    Vx(si) = 0.0
                    Dim bs As New BrentOpt.BrentMinimize
                    bs.DefineFuncDelegate(Function(tx)
                                              Hv = spp.DW_CalcEnthalpy(Vy, tx, P, State.Vapor)
                                              If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                  If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                      PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                                                  End If
                                              End If
                                              Return (H - Hv) ^ 2
                                          End Function)
                    Dim fmin As Double
                    fmin = bs.brentoptimize(Tmin, Tmaxs, 0.01, T)
                Else
                    Dim vf0 As Double, count As Integer
                    Do
                        vf0 = vf
                        vf = (H * spp.AUX_MMM(Vz) - Hlsat * spp.AUX_MMM(Vx)) / (Hvsat * spp.AUX_MMM(Vy) - Hlsat * spp.AUX_MMM(Vx))
                        lf = 1 - vf
                        Vy(nsi) = 1.0
                        Vx(nsi) = (Vz(nsi) - vf) / lf
                        Vx(si) = 1.0 - Vx(nsi)
                        Vxw = spp.AUX_CONVERT_MOL_TO_MASS(Vx)
                        x = Vxw(si)
                        count += 1
                    Loop Until Math.Abs(vf - vf0) < 0.000001 Or count > 50
                    If count > 50 Then Throw New Exception("unable to converge vapor fraction")
                    Dim bs As New BrentOpt.BrentMinimize
                    bs.DefineFuncDelegate(Function(tx)
                                              Hl = spp.DW_CalcEnthalpy(Vx, tx, P, State.Liquid)
                                              Hv = spp.DW_CalcEnthalpy(Vy, tx, P, State.Vapor)
                                              Dim mmv, mml As Double
                                              mmv = PP.AUX_MMM(Vy)
                                              mml = PP.AUX_MMM(Vx)
                                              Dim herr = H - mmv * vf / (mmv * vf + mml * lf) * Hv - mml * lf / (mmv * vf + mml * lf) * Hl
                                              If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                  If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                      PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                                                  End If
                                              End If
                                              Return herr ^ 2
                                          End Function)
                    Dim fmin As Double
                    fmin = bs.brentoptimize(Tmin, Tmaxs, 0.01, T)
                End If
                If vf > 1.0 Then
                    vf = 1.0
                    lf = 1 - vf
                    Vx(nsi) = 1.0
                    Vx(si) = 0.0
                End If

                If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                        PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                    End If
                End If

            End With

            Return New Object() {lf, vf, Vx, Vy, T, 0.0#, Vz.Clone, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides Function Flash_PS(ByVal Vz As Double(), ByVal P As Double, ByVal S As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim vf, vfant, lf, T, Tmin, Tmax, Tmaxs, Tsat, Tant, Sl, Sv, Slsat, Svsat As Double

            spp = PP

            Dim si = Array.IndexOf(spp.RET_VNAMES(), spp.SoluteCompound)
            Dim nsi = Array.IndexOf(spp.RET_VNAMES(), spp.SolventCompound)
            Dim xmax = spp.SolutionDataList(spp.SoluteName).xmax

            Dim Vxw = spp.AUX_CONVERT_MOL_TO_MASS(Vz)
            Dim x = Vxw(si)

            Tmin = CoolProp.Props1SI(spp.GetCoolPropName(x), "TMIN")
            Tmax = CoolProp.Props1SI(spp.GetCoolPropName(x), "TMAX")
            Tmaxs = CoolProp.Props1SI(spp.SolventCompound, "TMAX")

            Dim Vx = Vz.Clone
            Dim Vy = spp.RET_NullVector
            Vy(nsi) = 1.0

            T = Tref

            Tsat = spp.AUX_TSAT(P, x)

            With spp

                Slsat = spp.DW_CalcEntropy(Vz, Tsat, P, State.Liquid)
                Svsat = spp.DW_CalcEntropy(Vz, Tsat, P, State.Vapor)

                vfant = vf

                Tant = T
                If S <= Slsat Then
                    vf = 0.0#
                    lf = 1.0
                    Vx = Vz.Clone
                    Vy = PP.RET_NullVector
                    Vy(nsi) = 1.0
                    Dim bs As New BrentOpt.BrentMinimize
                    bs.DefineFuncDelegate(Function(tx)
                                              Sl = spp.DW_CalcEntropy(Vx, tx, P, State.Liquid)
                                              If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                  If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                      PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                                                  End If
                                              End If
                                              Return (S - Sl) ^ 2
                                          End Function)
                    Dim fmin As Double
                    fmin = bs.brentoptimize(Tmin, Tmax, 0.0001, T)
                ElseIf S >= Svsat Then
                    vf = 1.0#
                    lf = 1 - vf
                    Vy = Vz.Clone
                    Vx(nsi) = 1.0
                    Vx(si) = 0.0
                    Dim bs As New BrentOpt.BrentMinimize
                    bs.DefineFuncDelegate(Function(tx)
                                              Sv = spp.DW_CalcEntropy(Vy, tx, P, State.Vapor)
                                              If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                  If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                      PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                                                  End If
                                              End If
                                              Return (S - Sv) ^ 2
                                          End Function)
                    Dim fmin As Double
                    fmin = bs.brentoptimize(Tmin, Tmaxs, 0.01, T)
                Else
                    Dim vf0 As Double, count As Integer
                    Do
                        vf0 = vf
                        vf = (S * spp.AUX_MMM(Vz) - Slsat * spp.AUX_MMM(Vx)) / (Svsat * spp.AUX_MMM(Vy) - Slsat * spp.AUX_MMM(Vx))
                        lf = 1 - vf
                        Vy(nsi) = 1.0
                        Vx(nsi) = (Vz(nsi) - vf) / lf
                        Vx(si) = 1.0 - Vx(nsi)
                        Vxw = spp.AUX_CONVERT_MOL_TO_MASS(Vx)
                        x = Vxw(si)
                        count += 1
                    Loop Until Math.Abs(vf - vf0) < 0.000001 Or count > 50
                    If count > 50 Then Throw New Exception("unable to converge vapor fraction")
                    Dim bs As New BrentOpt.BrentMinimize
                    bs.DefineFuncDelegate(Function(tx)
                                              Sl = spp.DW_CalcEntropy(Vx, tx, P, State.Liquid)
                                              Sv = spp.DW_CalcEntropy(Vy, tx, P, State.Vapor)
                                              Dim mmv, mml As Double
                                              mmv = PP.AUX_MMM(Vy)
                                              mml = PP.AUX_MMM(Vx)
                                              Dim serr = S - mmv * vf / (mmv * vf + mml * lf) * Sv - mml * lf / (mmv * vf + mml * lf) * Sl
                                              If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                  If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                                                      PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                                                  End If
                                              End If
                                              Return serr ^ 2
                                          End Function)
                    Dim fmin As Double
                    fmin = bs.brentoptimize(Tmin, Tmaxs, 0.01, T)
                End If
                If vf > 1.0 Then
                    vf = 1.0
                    lf = 1 - vf
                    Vx(nsi) = 1.0
                    Vx(si) = 0.0
                End If

                If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                    If Not PP.CurrentMaterialStream.Flowsheet Is Nothing Then
                        PP.CurrentMaterialStream.Flowsheet.CheckStatus()
                    End If
                End If

            End With

            Return New Object() {lf, vf, Vx, Vy, T, 0.0#, Vz.Clone, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides Function Flash_TV(ByVal Vz As Double(), ByVal T As Double, ByVal V As Double, ByVal Pref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim vf, lf, P As Double

            spp = PP

            vf = V

            Dim si = Array.IndexOf(spp.RET_VNAMES(), spp.SoluteCompound)
            Dim nsi = Array.IndexOf(spp.RET_VNAMES(), spp.SolventCompound)

            Dim Vxw = spp.AUX_CONVERT_MOL_TO_MASS(Vz)
            Dim x = Vxw(si)

            P = CoolProp.PropsSI("P", "T", T, "Q", vf, spp.GetCoolPropName(x))

            lf = 1 - vf

            Dim Vx = Vz.Clone
            Dim Vy = spp.RET_NullVector

            Vy(si) = 1.0
            Vx(si) = (Vz(si) - vf) / lf
            Vx(nsi) = 1 - Vx(si)

            Return New Object() {lf, vf, Vx, Vy, P, 0.0#, Vz.Clone, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides Function Flash_PV(ByVal Vz As Double(), ByVal P As Double, ByVal V As Double, ByVal Tref As Double, ByVal PP As PropertyPackages.PropertyPackage, Optional ByVal ReuseKI As Boolean = False, Optional ByVal PrevKi As Double() = Nothing) As Object

            Dim vf, lf, T As Double

            spp = PP

            vf = V

            Dim si = Array.IndexOf(spp.RET_VNAMES(), spp.SoluteCompound)
            Dim nsi = Array.IndexOf(spp.RET_VNAMES(), spp.SolventCompound)

            Dim Vxw = spp.AUX_CONVERT_MOL_TO_MASS(Vz)
            Dim x = Vxw(si)

            T = spp.AUX_TSAT(P, x)

            lf = 1 - vf

            Dim Vx = Vz.Clone
            Dim Vy = spp.RET_NullVector

            Vy(si) = 1.0
            Vx(si) = (Vz(si) - vf) / lf
            Vx(nsi) = 1 - Vx(si)

            Return New Object() {lf, vf, Vx, Vy, T, 0.0#, Vz.Clone, 0.0#, PP.RET_NullVector, 0.0#, PP.RET_NullVector}

        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace
