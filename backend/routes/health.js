/**
 * Health Check Routes
 *
 * Provides endpoints to check API and database health status.
 */

const express = require('express');
const router = express.Router();
const db = require('../config/database');

/**
 * GET /api/health
 * Basic health check endpoint
 */
router.get('/', async (req, res) => {
  try {
    // Test database connection
    await db.query('SELECT 1');

    res.json({
      success: true,
      status: 'healthy',
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      database: 'connected'
    });
  } catch (error) {
    res.status(503).json({
      success: false,
      status: 'unhealthy',
      timestamp: new Date().toISOString(),
      database: 'disconnected',
      error: error.message
    });
  }
});

/**
 * GET /api/health/db
 * Detailed database health check
 */
router.get('/db', async (req, res) => {
  try {
    const [rows] = await db.query(`
      SELECT
        COUNT(*) as total_detections,
        MAX(created_at) as last_detection,
        MIN(created_at) as first_detection
      FROM detections
    `);

    res.json({
      success: true,
      database: {
        status: 'connected',
        host: process.env.DB_HOST,
        name: process.env.DB_NAME,
        stats: rows[0]
      },
      timestamp: new Date().toISOString()
    });
  } catch (error) {
    res.status(503).json({
      success: false,
      database: {
        status: 'error',
        error: error.message
      },
      timestamp: new Date().toISOString()
    });
  }
});

module.exports = router;
