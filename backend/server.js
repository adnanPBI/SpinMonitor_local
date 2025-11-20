/**
 * SpinMonitor Backend API Server
 *
 * Express.js backend that handles stream detection logging to MySQL.
 * Provides REST API endpoints for the SpinMonitor Desktop C# application.
 */

require('dotenv').config();
const express = require('express');
const cors = require('cors');
const helmet = require('helmet');
const compression = require('compression');
const rateLimit = require('express-rate-limit');

const db = require('./config/database');
const detectionsRouter = require('./routes/detections');
const healthRouter = require('./routes/health');

const app = express();
const PORT = process.env.PORT || 3000;

// Security middleware
app.use(helmet());

// CORS configuration
app.use(cors({
  origin: process.env.CORS_ORIGIN || '*',
  methods: ['GET', 'POST', 'PUT', 'DELETE'],
  allowedHeaders: ['Content-Type', 'Authorization', 'X-API-Key']
}));

// Compression middleware
app.use(compression());

// Body parsing middleware
app.use(express.json({ limit: '10mb' }));
app.use(express.urlencoded({ extended: true, limit: '10mb' }));

// Rate limiting
const limiter = rateLimit({
  windowMs: parseInt(process.env.RATE_LIMIT_WINDOW_MS) || 60000, // 1 minute
  max: parseInt(process.env.RATE_LIMIT_MAX_REQUESTS) || 100,
  message: 'Too many requests from this IP, please try again later.',
  standardHeaders: true,
  legacyHeaders: false,
});
app.use('/api/', limiter);

// Request logging middleware
app.use((req, res, next) => {
  const timestamp = new Date().toISOString();
  console.log(`[${timestamp}] ${req.method} ${req.path} - ${req.ip}`);
  next();
});

// API Key authentication middleware (optional)
const apiKeyAuth = (req, res, next) => {
  if (process.env.ENABLE_API_KEY_AUTH === 'true') {
    const apiKey = req.headers['x-api-key'];
    if (!apiKey || apiKey !== process.env.API_KEY) {
      return res.status(401).json({
        success: false,
        error: 'Unauthorized - Invalid or missing API key'
      });
    }
  }
  next();
};

// Routes
app.use('/api/health', healthRouter);
app.use('/api/detections', apiKeyAuth, detectionsRouter);

// Root endpoint
app.get('/', (req, res) => {
  res.json({
    name: 'SpinMonitor Backend API',
    version: '1.0.0',
    status: 'running',
    endpoints: {
      health: '/api/health',
      detections: '/api/detections'
    }
  });
});

// 404 handler
app.use((req, res) => {
  res.status(404).json({
    success: false,
    error: 'Endpoint not found'
  });
});

// Error handling middleware
app.use((err, req, res, next) => {
  console.error('Error:', err);
  res.status(err.status || 500).json({
    success: false,
    error: err.message || 'Internal server error',
    ...(process.env.NODE_ENV === 'development' && { stack: err.stack })
  });
});

// Graceful shutdown
process.on('SIGTERM', () => {
  console.log('SIGTERM signal received: closing HTTP server');
  server.close(() => {
    console.log('HTTP server closed');
    db.end(() => {
      console.log('Database connection closed');
      process.exit(0);
    });
  });
});

// Start server
const server = app.listen(PORT, () => {
  console.log('╔════════════════════════════════════════════════════════╗');
  console.log('║       SpinMonitor Backend API Server                  ║');
  console.log('╚════════════════════════════════════════════════════════╝');
  console.log(`\n✓ Server running on port ${PORT}`);
  console.log(`✓ Environment: ${process.env.NODE_ENV || 'development'}`);
  console.log(`✓ Database: ${process.env.DB_NAME}@${process.env.DB_HOST}:${process.env.DB_PORT}`);
  console.log(`✓ API Key Auth: ${process.env.ENABLE_API_KEY_AUTH === 'true' ? 'Enabled' : 'Disabled'}`);
  console.log(`\n📡 API Endpoints:`);
  console.log(`   - GET  /api/health`);
  console.log(`   - GET  /api/detections`);
  console.log(`   - POST /api/detections`);
  console.log(`   - GET  /api/detections/live`);
  console.log(`   - GET  /api/detections/stats`);
  console.log(`\n🚀 Ready to accept requests!\n`);
});

module.exports = app;
