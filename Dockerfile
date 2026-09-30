# syntax=docker/dockerfile:1
#
# Hai đích:
#   docker build --target runtime  -t salonsys-api .       máy chủ API
#   docker build --target migrator -t salonsys-migrate .   áp migration, chạy MỘT lần trước khi mở bản mới
#
# Máy chủ ngoài Development không tự migrate (Database:MigrateOnStartup), nên hai ảnh này đi thành
# cặp: chạy migrator xong rồi mới chạy runtime.

# ── Build ─────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Chép tệp dự án trước, restore, rồi mới chép mã nguồn: sửa một dòng mã không làm mất lớp cache
# của bước restore.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY NailManagement.Domain/NailManagement.Domain.csproj                 NailManagement.Domain/
COPY NailManagement.Application/NailManagement.Application.csproj       NailManagement.Application/
COPY NailManagement.Infrastructure/NailManagement.Infrastructure.csproj NailManagement.Infrastructure/
COPY NailManagement.API/NailManagement.API.csproj                       NailManagement.API/
RUN dotnet restore NailManagement.API/NailManagement.API.csproj

COPY NailManagement.Domain/         NailManagement.Domain/
COPY NailManagement.Application/    NailManagement.Application/
COPY NailManagement.Infrastructure/ NailManagement.Infrastructure/
COPY NailManagement.API/            NailManagement.API/

RUN dotnet publish NailManagement.API/NailManagement.API.csproj \
    --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

# ── Migration bundle ──────────────────────────────────────────────────────────
FROM build AS migrator-build
RUN dotnet tool install --global dotnet-ef --version 10.0.11
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project NailManagement.Infrastructure \
    --startup-project NailManagement.API \
    --configuration Release \
    --self-contained --target-runtime linux-x64 \
    --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app
COPY --from=migrator-build /app/efbundle .
USER $APP_UID
# Chuỗi kết nối truyền lúc chạy: docker run salonsys-migrate --connection "<chuỗi kết nối>"
ENTRYPOINT ["./efbundle"]

# ── Runtime ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
# Người dùng không phải root có sẵn trong ảnh chính thức của .NET.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "NailManagement.API.dll"]
