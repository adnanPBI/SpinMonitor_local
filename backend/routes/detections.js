/**
 * Detections Routes
 *
 * API endpoints for managing stream detection records.
 */

const express = require('express');
const router = express.Router();
const db = require('../config/database');

/**
 * POST /api/detections
 * Create a new detection record
 *
 * Body:
 * {
 *   "timestamp": "2025-01-20T12:30:45",
 *   "stream": "Radio Stream 1",
 *   "stream_type": "HTTP-MP3",
 *   "stream_number": "12345",
 *   "track": "Artist - Song Title",
 *   "duration_seconds": 10,
 *   "confidence": 0.95
 * }
 */
router.post('/', async (req, res) => {
  try {
    const {
      timestamp,
      stream,
      stream_type,
      stream_number,
      track,
      duration_seconds,
      confidence
    } = req.body;

    // Validation
    if (!timestamp || !stream || !track) {
      return res.status(400).json({
        success: false,
        error: 'Missing required fields: timestamp, stream, track'
      });
    }

    // Insert detection
    const [result] = await db.query(
      `INSERT INTO detections
       (timestamp, stream, stream_type, stream_number, track, duration_seconds, confidence)
       VALUES (?, ?, ?, ?, ?, ?, ?)`,
      [
        timestamp,
        stream,
        stream_type || null,
        stream_number || null,
        track,
        duration_seconds || 10,
        confidence || null
      ]
    );

    res.status(201).json({
      success: true,
      message: 'Detection created successfully',
      data: {
        id: result.insertId,
        timestamp,
        stream,
        stream_type,
        stream_number,
        track,
        duration_seconds,
        confidence
      }
    });

  } catch (error) {
    console.error('Error creating detection:', error);
    res.status(500).json({
      success: false,
      error: 'Failed to create detection',
      details: error.message
    });
  }
});

/**
 * POST /api/detections/batch
 * Create multiple detection records at once
 *
 * Body:
 * {
 *   "detections": [
 *     { "timestamp": "...", "stream": "...", "track": "..." },
 *     ...
 *   ]
 * }
 */
router.post('/batch', async (req, res) => {
  try {
    const { detections } = req.body;

    if (!Array.isArray(detections) || detections.length === 0) {
      return res.status(400).json({
        success: false,
        error: 'detections must be a non-empty array'
      });
    }

    // Validate all entries
    for (const detection of detections) {
      if (!detection.timestamp || !detection.stream || !detection.track) {
        return res.status(400).json({
          success: false,
          error: 'Each detection must have timestamp, stream, and track'
        });
      }
    }

    // Prepare batch insert
    const values = detections.map(d => [
      d.timestamp,
      d.stream,
      d.stream_type || null,
      d.stream_number || null,
      d.track,
      d.duration_seconds || 10,
      d.confidence || null
    ]);

    const [result] = await db.query(
      `INSERT INTO detections
       (timestamp, stream, stream_type, stream_number, track, duration_seconds, confidence)
       VALUES ?`,
      [values]
    );

    res.status(201).json({
      success: true,
      message: `${detections.length} detections created successfully`,
      data: {
        count: detections.length,
        firstId: result.insertId
      }
    });

  } catch (error) {
    console.error('Error creating batch detections:', error);
    res.status(500).json({
      success: false,
      error: 'Failed to create batch detections',
      details: error.message
    });
  }
});

/**
 * GET /api/detections
 * Get all detections with pagination and filtering
 *
 * Query params:
 *   - page: Page number (default: 1)
 *   - limit: Results per page (default: 50, max: 1000)
 *   - stream: Filter by stream name
 *   - track: Search in track name (partial match)
 *   - from: Start date (ISO 8601)
 *   - to: End date (ISO 8601)
 *   - min_confidence: Minimum confidence score
 *   - sort: Sort order (desc/asc, default: desc)
 */
router.get('/', async (req, res) => {
  try {
    const page = parseInt(req.query.page) || 1;
    const limit = Math.min(parseInt(req.query.limit) || 50, 1000);
    const offset = (page - 1) * limit;
    const sort = req.query.sort === 'asc' ? 'ASC' : 'DESC';

    // Build WHERE clause
    const conditions = [];
    const params = [];

    if (req.query.stream) {
      conditions.push('stream = ?');
      params.push(req.query.stream);
    }

    if (req.query.track) {
      conditions.push('track LIKE ?');
      params.push(`%${req.query.track}%`);
    }

    if (req.query.from) {
      conditions.push('timestamp >= ?');
      params.push(req.query.from);
    }

    if (req.query.to) {
      conditions.push('timestamp <= ?');
      params.push(req.query.to);
    }

    if (req.query.min_confidence) {
      conditions.push('confidence >= ?');
      params.push(parseFloat(req.query.min_confidence));
    }

    const whereClause = conditions.length > 0
      ? 'WHERE ' + conditions.join(' AND ')
      : '';

    // Get total count
    const [countResult] = await db.query(
      `SELECT COUNT(*) as total FROM detections ${whereClause}`,
      params
    );
    const total = countResult[0].total;

    // Get paginated results
    const [rows] = await db.query(
      `SELECT * FROM detections ${whereClause}
       ORDER BY timestamp ${sort}
       LIMIT ? OFFSET ?`,
      [...params, limit, offset]
    );

    res.json({
      success: true,
      data: rows,
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
        hasMore: offset + rows.length < total
      }
    });

  } catch (error) {
    console.error('Error fetching detections:', error);
    res.status(500).json({
      success: false,
      error: 'Failed to fetch detections',
      details: error.message
    });
  }
});

/**
 * GET /api/detections/live
 * Get recent detections (last 2 minutes) for "Live Now" panel
 *
 * Query params:
 *   - seconds: Time window in seconds (default: 120)
 */
router.get('/live', async (req, res) => {
  try {
    const seconds = parseInt(req.query.seconds) || 120;

    const [rows] = await db.query(
      `SELECT
         id,
         timestamp,
         stream,
         stream_type,
         stream_number,
         track,
         confidence,
         created_at
       FROM detections
       WHERE timestamp >= DATE_SUB(NOW(), INTERVAL ? SECOND)
       ORDER BY timestamp DESC`,
      [seconds]
    );

    res.json({
      success: true,
      data: rows,
      count: rows.length,
      timeWindow: `${seconds} seconds`
    });

  } catch (error) {
    console.error('Error fetching live detections:', error);
    res.status(500).json({
      success: false,
      error: 'Failed to fetch live detections',
      details: error.message
    });
  }
});

/**
 * GET /api/detections/stats
 * Get detection statistics
 *
 * Query params:
 *   - period: Time period (hour/day/week/month/year/all, default: day)
 */
router.get('/stats', async (req, res) => {
  try {
    const period = req.query.period || 'day';

    // Determine time range
    let timeCondition = '';
    switch (period) {
      case 'hour':
        timeCondition = 'WHERE timestamp >= DATE_SUB(NOW(), INTERVAL 1 HOUR)';
        break;
      case 'day':
        timeCondition = 'WHERE timestamp >= DATE_SUB(NOW(), INTERVAL 1 DAY)';
        break;
      case 'week':
        timeCondition = 'WHERE timestamp >= DATE_SUB(NOW(), INTERVAL 1 WEEK)';
        break;
      case 'month':
        timeCondition = 'WHERE timestamp >= DATE_SUB(NOW(), INTERVAL 1 MONTH)';
        break;
      case 'year':
        timeCondition = 'WHERE timestamp >= DATE_SUB(NOW(), INTERVAL 1 YEAR)';
        break;
      case 'all':
        timeCondition = '';
        break;
      default:
        timeCondition = 'WHERE timestamp >= DATE_SUB(NOW(), INTERVAL 1 DAY)';
    }

    // Get overall stats
    const [overall] = await db.query(
      `SELECT
         COUNT(*) as total_detections,
         COUNT(DISTINCT stream) as unique_streams,
         COUNT(DISTINCT track) as unique_tracks,
         AVG(confidence) as avg_confidence,
         MIN(timestamp) as first_detection,
         MAX(timestamp) as last_detection
       FROM detections ${timeCondition}`
    );

    // Get top streams
    const [topStreams] = await db.query(
      `SELECT
         stream,
         COUNT(*) as detection_count
       FROM detections ${timeCondition}
       GROUP BY stream
       ORDER BY detection_count DESC
       LIMIT 10`
    );

    // Get top tracks
    const [topTracks] = await db.query(
      `SELECT
         track,
         COUNT(*) as play_count,
         AVG(confidence) as avg_confidence
       FROM detections ${timeCondition}
       GROUP BY track
       ORDER BY play_count DESC
       LIMIT 10`
    );

    res.json({
      success: true,
      period,
      stats: overall[0],
      topStreams,
      topTracks
    });

  } catch (error) {
    console.error('Error fetching stats:', error);
    res.status(500).json({
      success: false,
      error: 'Failed to fetch statistics',
      details: error.message
    });
  }
});

/**
 * GET /api/detections/:id
 * Get a specific detection by ID
 */
router.get('/:id', async (req, res) => {
  try {
    const { id } = req.params;

    const [rows] = await db.query(
      'SELECT * FROM detections WHERE id = ?',
      [id]
    );

    if (rows.length === 0) {
      return res.status(404).json({
        success: false,
        error: 'Detection not found'
      });
    }

    res.json({
      success: true,
      data: rows[0]
    });

  } catch (error) {
    console.error('Error fetching detection:', error);
    res.status(500).json({
      success: false,
      error: 'Failed to fetch detection',
      details: error.message
    });
  }
});

/**
 * DELETE /api/detections/:id
 * Delete a specific detection by ID
 */
router.delete('/:id', async (req, res) => {
  try {
    const { id } = req.params;

    const [result] = await db.query(
      'DELETE FROM detections WHERE id = ?',
      [id]
    );

    if (result.affectedRows === 0) {
      return res.status(404).json({
        success: false,
        error: 'Detection not found'
      });
    }

    res.json({
      success: true,
      message: 'Detection deleted successfully'
    });

  } catch (error) {
    console.error('Error deleting detection:', error);
    res.status(500).json({
      success: false,
      error: 'Failed to delete detection',
      details: error.message
    });
  }
});

module.exports = router;
