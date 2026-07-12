# 베이스 이미지로 Ubuntu 20.04 LTS 사용 (멀티아치: amd64+arm64 지원)
FROM ubuntu:20.04

# 아치별 Unity 산출 디렉토리 (멀티아치 빌드 시 --build-arg BUILD_DIR=Build-<arch>)
ARG BUILD_DIR=Build

# 환경변수 설정
ENV DEBIAN_FRONTEND=noninteractive

# 필수 패키지 및 CA 인증서 설치
RUN apt-get update && \
    apt-get install -y \
    ca-certificates \
    libgtk2.0-0 \
    libglu1-mesa \
    libdrm2 \
    libgbm1 \
    libxrandr2 \
    libnss3 \
    libxcomposite1 \
    libxcursor1 \
    libasound2 \
    libxi6 \
    libxtst6 \
    libxrandr2 \
    xvfb \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# 작업 디렉토리 생성
WORKDIR /app

# 유니티로 빌드된 바이너리를 컨테이너의 /app 디렉토리에 복사 (BUILD_DIR = Build-<arch>)
COPY ${BUILD_DIR}/ /app/

# 실행 파일에 실행 권한 부여
RUN chmod +x /app/lop-server.x86_64

# 포트 설정 (게임에서 사용하는 포트에 따라 수정)
EXPOSE 7777

# 서버 실행
CMD ["/app/lop-server.x86_64", "-batchmode", "-nographics"]