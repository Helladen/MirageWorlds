﻿Imports System.IO
Imports System.Reflection
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.Metadata
Imports Microsoft.EntityFrameworkCore.Metadata.Internal
Imports Microsoft.Extensions.Configuration
Imports Mirage.Core.Database.DbContexts.Attributes
Imports Mirage.Core.Database.Types
Imports Pomelo.EntityFrameworkCore.MySql.Infrastructure

Namespace DbContexts
    Public Class MirageDbContext
        Inherits DbContext

        Private dbType As String
        Private tablePrefix As String
        Private connectionString As String
        Private ReadOnly configuration As IConfigurationRoot

        Public Property Accounts As DbSet(Of Account)
        Public Property Characters As DbSet(Of Character)

        Public Sub New()
            MyBase.New()

            Dim builder As IConfigurationBuilder = New ConfigurationBuilder() _
                .SetBasePath(AppContext.BaseDirectory) _
                .AddJsonFile("appsettings.database.json", optional:=False, reloadOnChange:=True) _
                .AddJsonFile($"appsettings.database.{If(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production")}.json", optional:=True, reloadOnChange:=True) _
                .AddUserSecrets(Of MirageDbContext)([optional]:=True, reloadOnChange:=True) _
                .AddEnvironmentVariables()

            Me.configuration = builder.Build()

            If String.IsNullOrWhiteSpace(Me.connectionString) Then
                Me.connectionString = Me.configuration("Database:ConnectionString")

                If String.IsNullOrWhiteSpace(Me.connectionString) Then
                    Me.connectionString = "Data Source=Database/Mirage.db"
                End If
            End If

            If String.IsNullOrWhiteSpace(Me.tablePrefix) Then
                Me.tablePrefix = Me.configuration("Database:TablePrefix")

                If String.IsNullOrWhiteSpace(Me.tablePrefix) Then
                    Me.tablePrefix = "mirage_worlds"
                End If
            End If

            If String.IsNullOrWhiteSpace(Me.dbType) Then
                Me.dbType = Me.configuration("Database:Type")

                If String.IsNullOrWhiteSpace(Me.dbType) Then
                    Me.dbType = "Sqlite"
                End If
            End If

            If Me.connectionString.Contains("Filename=") OrElse Me.connectionString.Contains("Data Source=") Then
                Dim dbdir As String = Path.GetDirectoryName(Me.connectionString.Replace("Filename=", "").Replace("Data Source=", ""))

                If Not String.IsNullOrWhiteSpace(dbdir) AndAlso Not Directory.Exists(dbdir) Then
                    Dim unused = Directory.CreateDirectory(dbdir)
                End If
            End If
        End Sub

        Protected Overrides Sub OnConfiguring(optionsBuilder As DbContextOptionsBuilder)
            Dim migrationsAssembly As String = GetType(MirageDbContext).Assembly.GetName().Name

            Select Case Trim$(Me.dbType.ToLower())
                Case "sqlite"
                    optionsBuilder = optionsBuilder.UseSqlite(Me.connectionString, Function(opt) opt.MigrationsAssembly(migrationsAssembly))
                Case "postgresql"
                    optionsBuilder = optionsBuilder.UseNpgsql(Me.connectionString, Function(opt) opt.MigrationsAssembly(migrationsAssembly))
                Case "mysql", "mariadb"
                    Dim dbVersion As String = Me.configuration("Database:Version")
                    Dim dbSqlType As ServerType = If(Me.dbType = "mariadb", ServerType.MariaDb, ServerType.MySql)
                    optionsBuilder = optionsBuilder.UseMySql(Me.connectionString, ServerVersion.Create(Version.Parse(dbVersion), dbSqlType), Function(opt) opt.MigrationsAssembly(migrationsAssembly))
                Case Else
                    Throw New NotSupportedException($"Database type '{Me.dbType}' is not supported.")
            End Select

            Console.WriteLine($"Database type '{Me.dbType}' has been configured.")
            MyBase.OnConfiguring(optionsBuilder)
        End Sub

        Protected Overrides Sub OnModelCreating(modelBuilder As ModelBuilder)
            For Each entityType In modelBuilder.Model.GetEntityTypes()
                Dim registerMethod = entityType.ClrType.GetMethod("RegisterComplexTypes", BindingFlags.Public Or BindingFlags.Static Or BindingFlags.FlattenHierarchy)
                Call registerMethod?.Invoke(Nothing, {modelBuilder})
            Next

            For Each entityType In modelBuilder.Model.GetEntityTypes()
                If Not entityType.IsOwned Then
                    For Each key In entityType.GetKeys()
                        For Each prop In key.Properties.Select(Function(p) p.PropertyInfo)
                            Call ValidatePrimaryKey(modelBuilder, entityType, prop)
                        Next
                    Next

                    For Each index In entityType.GetIndexes()
                        If Not entityType.GetKeys().Any(Function(k) index.Properties.SequenceEqual(k.Properties)) Then
                            For Each prop In index.Properties.Select(Function(p) p.PropertyInfo)
                                Call ValidatePrimaryKey(modelBuilder, entityType, prop)
                            Next
                        End If
                    Next

                    Call modelBuilder.Entity(entityType.ClrType).ToTable($"{Me.tablePrefix.ToLower()}_{entityType.GetTableName().ToLower()}")
                End If
            Next
        End Sub

        Protected Shared Function ValidatePrimaryKey(modelBuilder As ModelBuilder, entityType As IMutableEntityType, prop As PropertyInfo)
            Dim maxLengthAttribute = prop.GetCustomAttribute(Of MaxLengthAttribute)()

            If maxLengthAttribute IsNot Nothing Then
                Dim maxLength As Integer = If(maxLengthAttribute?.Length, 255)

                Select Case prop.PropertyType
                    Case GetType(Integer)
                        Return modelBuilder.Entity(entityType.ClrType).Property(prop.Name).HasColumnType($"INTEGER({maxLength})")
                    Case GetType(String)
                        Return modelBuilder.Entity(entityType.ClrType).Property(prop.Name).HasColumnType($"VARCHAR({maxLength})")
                    Case Else
                        Return modelBuilder.Entity(entityType.ClrType).Property(prop.Name).HasColumnType($"VARCHAR({maxLength})")
                End Select
            End If

            If prop.PropertyType Is GetType(String) Then
                Return modelBuilder.Entity(entityType.ClrType).Property(prop.Name).HasColumnType($"VARCHAR(255)")
            End If

            Return False
        End Function

        Public Function TrySaveChanges() As Boolean
            Try
                Call Me.SaveChanges()
                Return True
            Catch ex As Exception
                Console.WriteLine($"{ex.Message}")
                If ex.InnerException IsNot Nothing Then
                    Console.WriteLine($"{ex.InnerException.Message}")
                End If
                Return False
            End Try
        End Function

        Public Function UseConnectionString(connectionString As String) As MirageDbContext
            If String.IsNullOrWhiteSpace(connectionString) Then
                Throw New ArgumentException($"'{NameOf(connectionString)}' cannot be null or whitespace.", NameOf(connectionString))
            End If

            If Me.connectionString.Contains("Filename=") OrElse Me.connectionString.Contains("Data Source=") Then
                Dim dbdir As String = Path.GetDirectoryName(Me.connectionString.Replace("Filename=", "").Replace("Data Source=", ""))

                If Not String.IsNullOrWhiteSpace(dbdir) AndAlso Not Directory.Exists(dbdir) Then
                    Dim unused = Directory.CreateDirectory(dbdir)
                End If
            End If

            Me.connectionString = connectionString

            Return Me
        End Function

        Public Function UseDatabaseType(dbtype As String) As MirageDbContext
            If String.IsNullOrWhiteSpace(dbtype) Then
                Throw New ArgumentException($"'{NameOf(dbtype)}' cannot be null or whitespace.", NameOf(dbtype))
            End If

            Me.dbType = dbtype

            Return Me
        End Function

        Public Function UseTablePrefix(tablePrefix As String) As MirageDbContext
            If String.IsNullOrWhiteSpace(tablePrefix) Then
                Throw New ArgumentException($"'{NameOf(tablePrefix)}' cannot be null or whitespace.", NameOf(tablePrefix))
            End If

            Me.tablePrefix = tablePrefix

            Return Me
        End Function
    End Class
End Namespace