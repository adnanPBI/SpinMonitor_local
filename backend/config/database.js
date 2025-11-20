/**
 * MySQL Database Configuration
 *
 * Creates a connection pool to the MySQL database using mysql2.
 */

const mysql = require('mysql2');

// Create connection pool
const pool = mysql.createPool({
  host: process.env.DB_HOST || 'localhost',
  port: parseInt(process.env.DB_PORT) || 3306,
  database: process.env.DB_NAME || 'spinmonitor',
  user: process.env.DB_USER || 'root',
  password: process.env.DB_PASSWORD || '',
  waitForConnections: true,
  connectionLimit: 10,
  queueLimit: 0,
  enableKeepAlive: true,
  keepAliveInitialDelay: 0
});

// Test connection on startup
pool.getConnection((err, connection) => {
  if (err) {
    console.error('❌ Database connection failed:', err.message);
    if (err.code === 'PROTOCOL_CONNECTION_LOST') {
      console.error('   Database connection was closed.');
    }
    if (err.code === 'ER_CON_COUNT_ERROR') {
      console.error('   Database has too many connections.');
    }
    if (err.code === 'ECONNREFUSED') {
      console.error('   Database connection was refused.');
    }
  } else {
    console.log('✓ Database connection established');
    connection.release();
  }
});

// Promisify for async/await
const promisePool = pool.promise();

module.exports = promisePool;
