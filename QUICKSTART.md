# Klyvesta AI-Native Trading Platform - Quick Start Guide

## Prerequisites
- Docker & Docker Compose installed
- .NET 8 SDK (for local development)
- PostgreSQL client (optional, for direct DB access)

## Quick Start (Paper Mode)

### 1. Clone and Navigate
```bash
cd /workspace
```

### 2. Set Environment Variables
Create a `.env` file in the root directory:
```bash
cat > .env << EOF
DB_PASSWORD=SecurePass123!
PAYLOAD_SIGNING_KEY=YourSuperSecretKeyChangeInProduction
GRAFANA_PASSWORD=admin123
EOF
```

### 3. Launch with Docker Compose
```bash
docker-compose up -d
```

This will start:
- **Klyvesta API** on `http://localhost:5000`
- **PostgreSQL Database** on `localhost:5432`
- **Redis Cache** on `localhost:6379`
- **Prometheus Monitoring** on `http://localhost:9090`
- **Grafana Dashboard** on `http://localhost:3000` (admin/admin123)

### 4. Verify System Health
```bash
curl http://localhost:5000/health
```

Expected response:
```json
{
  "status": "Healthy",
  "components": {
    "database": "Healthy",
    "broker": "Healthy (Paper Mode)",
    "redis": "Healthy"
  }
}
```

### 5. Run Initial Migration
```bash
docker-compose exec klyvesta-api dotnet ef database update
```

### 6. Access Grafana Dashboard
Open `http://localhost:3000` and login with:
- Username: `admin`
- Password: `admin123` (or your custom password from `.env`)

Navigate to the pre-configured "Klyvesta Main Dashboard" to view:
- System health status
- Request rates
- Order submission metrics
- Risk rejection counts
- Active alerts

## Testing the System

### Submit a Test Order (Paper Mode)
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Signature: test-signature" \
  -d '{
    "customerId": "cust_001",
    "symbol": "PSX100",
    "quantity": 100,
    "side": "Buy",
    "orderType": "Market",
    "idempotencyKey": "test-order-001"
  }'
```

### Check Order Status
```bash
curl http://localhost:5000/api/orders/test-order-001
```

### View Portfolio
```bash
curl http://localhost:5000/api/portfolio/cust_001
```

## Switching to Live Mode

**⚠️ WARNING**: Only switch to Live Mode after receiving valid broker API credentials and completing sandbox testing.

### 1. Update Configuration
Edit `.env`:
```bash
BROKER_MODE=Live
BROKER_API_KEY=your_live_api_key
BROKER_API_SECRET=your_live_secret
BROKER_API_ENDPOINT=https://api.pyPSX.com/v1
```

### 2. Restart Services
```bash
docker-compose down
docker-compose up -d
```

### 3. Verify Broker Connection
```bash
curl http://localhost:5000/health
```

Check that `"broker": "Healthy (Live Mode)"` appears in the response.

## Development Mode

For local development without Docker:

### 1. Install Dependencies
```bash
dotnet restore
```

### 2. Run Migrations
```bash
dotnet ef database update --project src/Klyvesta.Infrastructure --startup-project src/Klyvesta.Api
```

### 3. Start API
```bash
dotnet run --project src/Klyvesta.Api
```

### 4. Run Tests
```bash
dotnet test
```

## Troubleshooting

### Database Connection Issues
```bash
docker-compose logs db
```

### API Errors
```bash
docker-compose logs klyvesta-api
```

### Reset Database (Development Only)
```bash
docker-compose down -v
docker-compose up -d db
# Wait for DB to initialize, then restart API
docker-compose up -d klyvesta-api
```

## Next Steps

1. **Onboard First Customer**: Use the `/api/onboarding` endpoints to register a test customer
2. **Grant Mandate**: Complete the digital mandate flow for auto-trading
3. **Enable AI Shadow Mode**: Observe AI recommendations without execution
4. **Monitor Performance**: Watch Grafana dashboards for system metrics
5. **Review Compliance Logs**: Audit all trades and risk decisions

## Support

- **Documentation**: See `/docs` folder for detailed guides
- **Operations Runbook**: `docs/OPERATIONS_RUNBOOK.md`
- **Compliance Dossier**: `docs/COMPLIANCE_DOSSIER.md`
- **Architecture**: `docs/ARCHITECTURE_DECISION_RECORD.md`

---

**System Status**: ✅ Production Ready (Paper Mode)  
**Live Mode**: ⏳ Awaiting Broker API Credentials  
**Last Updated**: 2026-09-12
